using KingraPOS.Application.Abstractions.Services;
using KingraPOS.Application.Dtos;
using KingraPOS.Application.Security;
using KingraPOS.Application.Stock;
using KingraPOS.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace KingraPOS.Tests;

public class StockServiceTests
{
    [Fact]
    public async Task Opening_stock_creates_the_balance_and_an_opening_mutation()
    {
        using var database = new TestDatabase();
        await database.InitializeAsync();

        var (productId, _) = await CreateProductAsync(database);
        var stockService = database.GetRequiredService<IStockService>();

        await stockService.RecordAdjustmentAsync(new StockAdjustmentRequest(
            StockAdjustmentKind.Opening,
            new[] { new StockAdjustmentItemRequest(productId, null, 10_000, null) },
            Notes: "Stok awal"));

        var inventory = await stockService.GetInventoryAsync(productId);

        Assert.Equal(10_000, inventory.StockQuantity);

        using var context = database.CreateContext();
        var mutation = await context.StockMutations.SingleAsync();

        Assert.Equal(StockMutationType.OPENING, mutation.MutationType);
        Assert.Equal(StockBucket.AVAILABLE, mutation.StockBucket);
        Assert.Equal(10_000, mutation.Quantity);
        Assert.Equal(0, mutation.StockBefore);
        Assert.Equal(10_000, mutation.StockAfter);

        var adjustment = await context.StockAdjustments.SingleAsync();
        Assert.Equal(StockAdjustmentReason.OTHER, adjustment.Reason);
    }

    [Fact]
    public async Task Stock_in_converts_the_selected_unit_into_base_units()
    {
        using var database = new TestDatabase();
        await database.InitializeAsync();

        var (productId, _) = await CreateProductAsync(database);
        var dusId = await database.SeedUnitAsync("Dus");

        var productService = database.GetRequiredService<IProductService>();
        var dusUnit = await productService.AddUnitAsync(productId, new ProductUnitRequest(dusId, null, 12_000));

        var stockService = database.GetRequiredService<IStockService>();

        await stockService.RecordAdjustmentAsync(new StockAdjustmentRequest(
            StockAdjustmentKind.StockIn,
            new[] { new StockAdjustmentItemRequest(productId, dusUnit.Id, 1_000, null) }));

        // 1 Dus (dikonversi) = 12 Pcs
        Assert.Equal(12_000, (await stockService.GetInventoryAsync(productId)).StockQuantity);
    }

    [Fact]
    public async Task Stock_in_with_purchase_price_updates_the_moving_average_cost()
    {
        using var database = new TestDatabase();
        await database.InitializeAsync();

        var (productId, _) = await CreateProductAsync(database);
        var stockService = database.GetRequiredService<IStockService>();
        var productService = database.GetRequiredService<IProductService>();

        await stockService.RecordAdjustmentAsync(new StockAdjustmentRequest(
            StockAdjustmentKind.Opening,
            new[] { new StockAdjustmentItemRequest(productId, null, 10_000, null) }));

        // HPP awal 1.000
        await productService.UpdateAsync(productId, new UpdateProductRequest(
            "SKU-001", "Keripik", null, null, 1_000, null, true, true));

        await stockService.RecordAdjustmentAsync(new StockAdjustmentRequest(
            StockAdjustmentKind.StockIn,
            new[] { new StockAdjustmentItemRequest(productId, null, 10_000, 2_000) }));

        var detail = await productService.GetDetailAsync(productId);

        // (10.000 x 1.000) + (10.000 x 2.000) = 30.000.000 / 20.000 = 1.500
        Assert.Equal(1_500, detail.BaseCostPrice);
        Assert.Equal(20_000, (await stockService.GetInventoryAsync(productId)).StockQuantity);
    }

    [Fact]
    public async Task Stock_in_without_cost_keeps_the_current_cost()
    {
        using var database = new TestDatabase();
        await database.InitializeAsync();

        var (productId, _) = await CreateProductAsync(database);
        var stockService = database.GetRequiredService<IStockService>();
        var productService = database.GetRequiredService<IProductService>();

        await productService.UpdateAsync(productId, new UpdateProductRequest(
            "SKU-001", "Keripik", null, null, 1_000, null, true, true));

        await stockService.RecordAdjustmentAsync(new StockAdjustmentRequest(
            StockAdjustmentKind.StockIn,
            new[] { new StockAdjustmentItemRequest(productId, null, 5_000, null) }));

        Assert.Equal(1_000, (await productService.GetDetailAsync(productId)).BaseCostPrice);
    }

    [Fact]
    public async Task Cost_price_is_ignored_for_users_without_the_permission()
    {
        using var database = new TestDatabase();
        await database.InitializeAsync();

        var (productId, _) = await CreateProductAsync(database);

        var productService = database.GetRequiredService<IProductService>();
        await productService.UpdateAsync(productId, new UpdateProductRequest(
            "SKU-001", "Keripik", null, null, 1_000, null, true, true));

        await LoginAsCashierWithStockPermissionsAsync(database);

        var stockService = database.GetRequiredService<IStockService>();

        await stockService.RecordAdjustmentAsync(new StockAdjustmentRequest(
            StockAdjustmentKind.StockIn,
            new[] { new StockAdjustmentItemRequest(productId, null, 10_000, 9_000) }));

        // Kasir tidak punya product.view_cost_price, jadi harga beli diabaikan dan HPP tidak berubah.
        // Dibaca langsung dari database karena DTO menyembunyikan HPP untuk kasir.
        using var context = database.CreateContext();

        var product = await context.Products.SingleAsync(row => row.Id == productId);

        Assert.Equal(1_000, product.BaseCostPrice);
        Assert.Null((await context.StockAdjustmentItems.SingleAsync()).CostPrice);
    }

    [Fact]
    public async Task Stock_count_sets_the_absolute_quantity()
    {
        using var database = new TestDatabase();
        await database.InitializeAsync();

        var (productId, _) = await CreateProductAsync(database);
        var stockService = database.GetRequiredService<IStockService>();

        await stockService.RecordAdjustmentAsync(new StockAdjustmentRequest(
            StockAdjustmentKind.Opening,
            new[] { new StockAdjustmentItemRequest(productId, null, 10_000, null) }));

        await stockService.RecordAdjustmentAsync(new StockAdjustmentRequest(
            StockAdjustmentKind.StockCount,
            new[] { new StockAdjustmentItemRequest(productId, null, 7_000, null) }));

        Assert.Equal(7_000, (await stockService.GetInventoryAsync(productId)).StockQuantity);

        using var context = database.CreateContext();
        var item = await context.StockAdjustmentItems
            .OrderByDescending(row => row.QuantityBefore)
            .FirstAsync();

        Assert.Equal(10_000, item.QuantityBefore);
        Assert.Equal(7_000, item.QuantityAfter);
        Assert.Equal(-3_000, item.Difference);
    }

    [Fact]
    public async Task Negative_stock_is_rejected_unless_the_setting_allows_it()
    {
        using var database = new TestDatabase();
        await database.InitializeAsync();

        var (productId, _) = await CreateProductAsync(database);
        var stockService = database.GetRequiredService<IStockService>();

        await Assert.ThrowsAsync<InvalidOperationException>(() => stockService.RecordAdjustmentAsync(
            new StockAdjustmentRequest(
                StockAdjustmentKind.StockCount,
                new[] { new StockAdjustmentItemRequest(productId, null, -1_000, null) })));

        using (var context = database.CreateContext())
        {
            var settings = await context.Settings.SingleAsync();
            settings.AllowNegativeStock = true;
            await context.SaveChangesAsync();
        }

        await stockService.RecordAdjustmentAsync(new StockAdjustmentRequest(
            StockAdjustmentKind.StockCount,
            new[] { new StockAdjustmentItemRequest(productId, null, -1_000, null) }));

        Assert.Equal(-1_000, (await stockService.GetInventoryAsync(productId)).StockQuantity);
    }

    [Fact]
    public async Task Reconciliation_stock_balance_equals_the_sum_of_mutations()
    {
        using var database = new TestDatabase();
        await database.InitializeAsync();

        var first = await CreateProductAsync(database, "SKU-001", "Keripik");
        var second = await CreateProductAsync(database, "SKU-002", "Teh");

        var stockService = database.GetRequiredService<IStockService>();

        await stockService.RecordAdjustmentAsync(new StockAdjustmentRequest(
            StockAdjustmentKind.Opening,
            new[]
            {
                new StockAdjustmentItemRequest(first.ProductId, null, 10_000, null),
                new StockAdjustmentItemRequest(second.ProductId, null, 5_000, null)
            }));

        await stockService.RecordAdjustmentAsync(new StockAdjustmentRequest(
            StockAdjustmentKind.StockIn,
            new[] { new StockAdjustmentItemRequest(first.ProductId, null, 4_000, 1_200) },
            Notes: "Barang masuk"));

        await stockService.RecordAdjustmentAsync(new StockAdjustmentRequest(
            StockAdjustmentKind.StockCount,
            new[] { new StockAdjustmentItemRequest(first.ProductId, null, 13_000, null) }));

        await stockService.RecordAdjustmentAsync(new StockAdjustmentRequest(
            StockAdjustmentKind.Lost,
            new[] { new StockAdjustmentItemRequest(second.ProductId, null, 4_500, null) }));

        using var context = database.CreateContext();

        var balances = await context.Inventories.ToListAsync();
        Assert.Equal(2, balances.Count);

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

        // Angka konkret: produk pertama 13.000, produk kedua 4.500
        Assert.Equal(13_000, balances.Single(row => row.ProductId == first.ProductId).StockQuantity);
        Assert.Equal(4_500, balances.Single(row => row.ProductId == second.ProductId).StockQuantity);
    }

    [Fact]
    public async Task Stock_in_requires_the_receive_permission()
    {
        using var database = new TestDatabase();
        await database.InitializeAsync();

        var (productId, _) = await CreateProductAsync(database);

        await LoginAsCashierWithStockPermissionsAsync(database, includeReceive: false);

        var stockService = database.GetRequiredService<IStockService>();

        // stock.update ada, tetapi stock.receive tidak
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => stockService.RecordAdjustmentAsync(
            new StockAdjustmentRequest(
                StockAdjustmentKind.StockIn,
                new[] { new StockAdjustmentItemRequest(productId, null, 1_000, null) })));

        await stockService.RecordAdjustmentAsync(new StockAdjustmentRequest(
            StockAdjustmentKind.Opening,
            new[] { new StockAdjustmentItemRequest(productId, null, 1_000, null) }));
    }

    [Fact]
    public async Task Adjustments_are_rejected_for_products_that_do_not_track_stock()
    {
        using var database = new TestDatabase();
        await database.InitializeAsync();

        var unitId = await database.SeedUnitAsync("Pcs");

        var created = await database.GetRequiredService<IProductService>().CreateAsync(
            new CreateProductRequest("SKU-JASA", "Jasa Pasang", null, unitId, null, null, 15_000, false));

        var stockService = database.GetRequiredService<IStockService>();

        await Assert.ThrowsAsync<InvalidOperationException>(() => stockService.RecordAdjustmentAsync(
            new StockAdjustmentRequest(
                StockAdjustmentKind.Opening,
                new[] { new StockAdjustmentItemRequest(created.Id, null, 1_000, null) })));
    }

    [Fact]
    public async Task Mutation_history_requires_the_history_permission()
    {
        using var database = new TestDatabase();
        await database.InitializeAsync();

        var stockService = database.GetRequiredService<IStockService>();
        Assert.Empty(await stockService.GetMutationsAsync());

        await LoginAsCashierWithStockPermissionsAsync(database);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => stockService.GetMutationsAsync());
    }

    [Fact]
    public async Task Low_stock_flag_uses_the_threshold()
    {
        using var database = new TestDatabase();
        await database.InitializeAsync();

        var (productId, _) = await CreateProductAsync(database);
        var stockService = database.GetRequiredService<IStockService>();

        await stockService.RecordAdjustmentAsync(new StockAdjustmentRequest(
            StockAdjustmentKind.Opening,
            new[] { new StockAdjustmentItemRequest(productId, null, 3_000, null) }));

        // ambang default 5 satuan
        Assert.True((await stockService.GetInventoryAsync(productId)).IsLowStock);
        Assert.Single(await stockService.GetInventoriesAsync(lowStockOnly: true));

        await stockService.SetLowStockThresholdAsync(productId, 1_000);

        Assert.False((await stockService.GetInventoryAsync(productId)).IsLowStock);
        Assert.Empty(await stockService.GetInventoriesAsync(lowStockOnly: true));
    }

    private static async Task<(string ProductId, string UnitId)> CreateProductAsync(
        TestDatabase database,
        string sku = "SKU-001",
        string name = "Keripik")
    {
        var unitId = await database.SeedUnitAsync("Pcs");

        var created = await database.GetRequiredService<IProductService>().CreateAsync(
            new CreateProductRequest(sku, name, null, unitId, null, null, null, true));

        return (created.Id, unitId);
    }

    private static async Task LoginAsCashierWithStockPermissionsAsync(
        TestDatabase database,
        bool includeReceive = true)
    {
        var userService = database.GetRequiredService<IUserManagementService>();
        var roleService = database.GetRequiredService<IRoleManagementService>();

        var cashierRoleId = (await userService.GetRolesAsync())
            .Single(role => role.Name == PermissionCatalog.CashierRoleName).Id;

        var cashier = await userService.CreateUserAsync(new CreateUserRequest(
            "Kasir Satu",
            "kasir1",
            "rahasia123",
            cashierRoleId,
            null));

        await roleService.SetUserOverrideAsync(cashier.Id, PermissionCatalog.StockUpdate, PermissionOverrideState.Allow);

        if (includeReceive)
            await roleService.SetUserOverrideAsync(cashier.Id, PermissionCatalog.StockReceive, PermissionOverrideState.Allow);

        await database.GetRequiredService<IAuthenticationService>()
            .LoginAsync(new LoginRequest("kasir1", "rahasia123"));
    }
}
