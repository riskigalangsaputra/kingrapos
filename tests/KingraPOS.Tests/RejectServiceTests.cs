using KingraPOS.Application.Abstractions.Services;
using KingraPOS.Application.Dtos;
using KingraPOS.Application.Security;
using KingraPOS.Application.Stock;
using KingraPOS.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace KingraPOS.Tests;

public class RejectServiceTests
{
    [Fact]
    public async Task Reject_moves_stock_between_buckets_and_writes_one_mutation_per_bucket()
    {
        using var database = new TestDatabase();
        await database.InitializeAsync();

        var (productId, _) = await CreateProductWithStockAsync(database, 10_000, baseCostPrice: 1_500);

        var stockService = database.GetRequiredService<IStockService>();

        await stockService.RecordRejectAsync(new RejectRequest(productId, 2_000, "RUSAK", Notes: "Pecah"));

        var inventory = await stockService.GetInventoryAsync(productId);

        Assert.Equal(8_000, inventory.StockQuantity);
        Assert.Equal(2_000, inventory.RejectQuantity);

        using var context = database.CreateContext();

        var mutations = await context.StockMutations
            .Where(mutation => mutation.MutationType == StockMutationType.REJECT)
            .OrderBy(mutation => mutation.StockBucket)
            .ToListAsync();

        Assert.Equal(2, mutations.Count);

        var available = mutations.Single(mutation => mutation.StockBucket == StockBucket.AVAILABLE);
        Assert.Equal(-2_000, available.Quantity);
        Assert.Equal(10_000, available.StockBefore);
        Assert.Equal(8_000, available.StockAfter);
        Assert.Equal(StockReferenceType.REJECT_LOG, available.ReferenceType);

        var reject = mutations.Single(mutation => mutation.StockBucket == StockBucket.REJECT);
        Assert.Equal(2_000, reject.Quantity);
        Assert.Equal(0, reject.StockBefore);
        Assert.Equal(2_000, reject.StockAfter);

        var log = await context.ProductRejectLogs.SingleAsync();
        Assert.Equal(RejectStatus.REJECTED_IN_STORE, log.Status);
        Assert.Equal(1_500, log.CostPriceAtIncident);
        Assert.Equal(3_000, log.TotalLossAmount); // 1.500 x 2 satuan
        Assert.Null(log.ResolvedAt);
    }

    [Fact]
    public async Task Reject_cannot_exceed_the_available_stock()
    {
        using var database = new TestDatabase();
        await database.InitializeAsync();

        var (productId, _) = await CreateProductWithStockAsync(database, 1_000);

        var stockService = database.GetRequiredService<IStockService>();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => stockService.RecordRejectAsync(new RejectRequest(productId, 2_000, "RUSAK")));
    }

    [Fact]
    public async Task Reject_requires_a_reason_and_positive_quantity()
    {
        using var database = new TestDatabase();
        await database.InitializeAsync();

        var (productId, _) = await CreateProductWithStockAsync(database, 5_000);
        var stockService = database.GetRequiredService<IStockService>();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => stockService.RecordRejectAsync(new RejectRequest(productId, 0, "RUSAK")));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => stockService.RecordRejectAsync(new RejectRequest(productId, 1_000, "   ")));
    }

    [Fact]
    public async Task Resolving_as_returned_to_supplier_clears_the_reject_balance()
    {
        using var database = new TestDatabase();
        await database.InitializeAsync();

        var (productId, _) = await CreateProductWithStockAsync(database, 10_000);
        var stockService = database.GetRequiredService<IStockService>();

        var rejectId = await stockService.RecordRejectAsync(new RejectRequest(productId, 3_000, "CACAT"));

        await stockService.ResolveRejectAsync(rejectId, RejectResolution.ReturnedToSupplier);

        var inventory = await stockService.GetInventoryAsync(productId);

        Assert.Equal(7_000, inventory.StockQuantity);
        Assert.Equal(0, inventory.RejectQuantity);

        using var context = database.CreateContext();
        var log = await context.ProductRejectLogs.SingleAsync();

        Assert.Equal(RejectStatus.RETURNED_TO_SUPPLIER, log.Status);
        Assert.NotNull(log.ResolvedAt);
        Assert.NotNull(log.ResolvedByUserId);

        await AssertReconciliationAsync(context);
    }

    [Fact]
    public async Task Resolving_as_written_off_clears_the_reject_balance()
    {
        using var database = new TestDatabase();
        await database.InitializeAsync();

        var (productId, _) = await CreateProductWithStockAsync(database, 10_000);
        var stockService = database.GetRequiredService<IStockService>();

        var rejectId = await stockService.RecordRejectAsync(new RejectRequest(productId, 2_500, "HILANG"));

        await stockService.ResolveRejectAsync(rejectId, RejectResolution.WrittenOff);

        Assert.Equal(0, (await stockService.GetInventoryAsync(productId)).RejectQuantity);

        using var context = database.CreateContext();
        Assert.Equal(RejectStatus.WRITTEN_OFF, (await context.ProductRejectLogs.SingleAsync()).Status);

        await AssertReconciliationAsync(context);
    }

    [Fact]
    public async Task A_reject_log_can_only_be_resolved_once()
    {
        using var database = new TestDatabase();
        await database.InitializeAsync();

        var (productId, _) = await CreateProductWithStockAsync(database, 10_000);
        var stockService = database.GetRequiredService<IStockService>();

        var rejectId = await stockService.RecordRejectAsync(new RejectRequest(productId, 1_000, "RUSAK"));

        await stockService.ResolveRejectAsync(rejectId, RejectResolution.WrittenOff);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => stockService.ResolveRejectAsync(rejectId, RejectResolution.ReturnedToSupplier));
    }

    [Fact]
    public async Task Open_filter_only_returns_unresolved_logs()
    {
        using var database = new TestDatabase();
        await database.InitializeAsync();

        var (productId, _) = await CreateProductWithStockAsync(database, 10_000);
        var stockService = database.GetRequiredService<IStockService>();

        var first = await stockService.RecordRejectAsync(new RejectRequest(productId, 1_000, "RUSAK"));
        await stockService.RecordRejectAsync(new RejectRequest(productId, 1_000, "CACAT"));

        await stockService.ResolveRejectAsync(first, RejectResolution.WrittenOff);

        Assert.Equal(2, (await stockService.GetRejectLogsAsync()).Count);
        Assert.Single(await stockService.GetRejectLogsAsync(openOnly: true));
    }

    [Fact]
    public async Task Reject_requires_the_stock_reject_permission()
    {
        using var database = new TestDatabase();
        await database.InitializeAsync();

        var (productId, _) = await CreateProductWithStockAsync(database, 5_000);

        await LoginAsUserWithoutRejectPermissionAsync(database);

        var stockService = database.GetRequiredService<IStockService>();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => stockService.RecordRejectAsync(new RejectRequest(productId, 1_000, "RUSAK")));
    }

    private static async Task AssertReconciliationAsync(KingraPOS.Application.Abstractions.Persistence.IKingraPosDbContext context)
    {
        var balances = await context.Inventories.ToListAsync();

        foreach (var balance in balances)
        {
            var available = await context.StockMutations
                .Where(mutation => mutation.ProductId == balance.ProductId
                                   && mutation.StockBucket == StockBucket.AVAILABLE)
                .SumAsync(mutation => (long?)mutation.Quantity) ?? 0;

            var reject = await context.StockMutations
                .Where(mutation => mutation.ProductId == balance.ProductId
                                   && mutation.StockBucket == StockBucket.REJECT)
                .SumAsync(mutation => (long?)mutation.Quantity) ?? 0;

            Assert.Equal(balance.StockQuantity, available);
            Assert.Equal(balance.RejectQuantity, reject);
        }
    }

    private static async Task<(string ProductId, string UnitId)> CreateProductWithStockAsync(
        TestDatabase database,
        long quantity,
        long? baseCostPrice = null)
    {
        var unitId = await database.SeedUnitAsync("Pcs");

        var productService = database.GetRequiredService<IProductService>();

        var created = await productService.CreateAsync(
            new CreateProductRequest("SKU-001", "Keripik", null, unitId, null, null, null, true));

        if (baseCostPrice.HasValue)
        {
            await productService.UpdateAsync(created.Id, new UpdateProductRequest(
                "SKU-001", "Keripik", null, null, baseCostPrice, null, true, true));
        }

        await database.GetRequiredService<IStockService>().RecordAdjustmentAsync(
            new StockAdjustmentRequest(
                StockAdjustmentKind.Opening,
                new[] { new StockAdjustmentItemRequest(created.Id, null, quantity, null) }));

        return (created.Id, unitId);
    }

    private static async Task LoginAsUserWithoutRejectPermissionAsync(TestDatabase database)
    {
        var userService = database.GetRequiredService<IUserManagementService>();

        var cashierRoleId = (await userService.GetRolesAsync())
            .Single(role => role.Name == PermissionCatalog.CashierRoleName).Id;

        await userService.CreateUserAsync(new CreateUserRequest(
            "Kasir Satu", "kasir1", "rahasia123", cashierRoleId, null));

        await database.GetRequiredService<IAuthenticationService>()
            .LoginAsync(new LoginRequest("kasir1", "rahasia123"));
    }
}
