using KingraPOS.Application.Abstractions.Services;
using KingraPOS.Application.Dtos;
using KingraPOS.Application.Security;
using KingraPOS.Application.Stock;
using KingraPOS.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace KingraPOS.Tests;

public class TransactionServiceTests
{
    [Fact]
    public async Task Checkout_requires_an_open_shift()
    {
        using var database = new TestDatabase();
        await database.InitializeAsync();

        var (productId, _) = await CreateProductAsync(database);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            database.GetRequiredService<ITransactionService>().CheckoutAsync(new CheckoutRequest(
                new[] { new CartLineRequest(productId, null, 1_000) },
                "CASH",
                AmountPaid: 100_000)));
    }

    [Fact]
    public async Task Checkout_decreases_stock_and_writes_a_sale_mutation()
    {
        using var database = new TestDatabase();
        await database.InitializeAsync();

        var (productId, _) = await CreateProductAsync(database);
        await OpenShiftAsync(database);

        var transaction = await database.GetRequiredService<ITransactionService>().CheckoutAsync(
            new CheckoutRequest(
                new[] { new CartLineRequest(productId, null, 2_000) },
                "CASH",
                AmountPaid: 100_000));

        var inventory = await database.GetRequiredService<IStockService>().GetInventoryAsync(productId);
        Assert.Equal(18_000, inventory.StockQuantity);

        using var context = database.CreateContext();
        var mutation = await context.StockMutations.SingleAsync(row => row.MutationType == StockMutationType.SALE);

        Assert.Equal(StockMutationType.SALE, mutation.MutationType);
        Assert.Equal(StockBucket.AVAILABLE, mutation.StockBucket);
        Assert.Equal(-2_000, mutation.Quantity);
        Assert.Equal(StockReferenceType.TRANSACTION, mutation.ReferenceType);
        Assert.Equal(transaction.Id, mutation.ReferenceId);
        Assert.Single(transaction.Items);
    }

    [Fact]
    public async Task Checkout_applies_the_tiered_price_for_the_quantity()
    {
        using var database = new TestDatabase();
        await database.InitializeAsync();

        var (productId, _) = await CreateProductAsync(database);
        await OpenShiftAsync(database);

        var productService = database.GetRequiredService<IProductService>();
        var productUnitId = (await productService.GetDetailAsync(productId)).Units.First().Id;

        await productService.SetTiersAsync(productUnitId, new[]
        {
            new ProductPriceTierRequest("Grosir", 10_000, 4_500)
        });

        // 12 Pcs memenuhi tier minimum 10 Pcs.
        var quote = await database.GetRequiredService<ITransactionService>().QuoteAsync(
            new CheckoutRequest(
                new[] { new CartLineRequest(productId, null, 12_000) },
                "CASH"));

        Assert.Equal(54_000, quote.ItemsAmount);

        var transaction = await database.GetRequiredService<ITransactionService>().CheckoutAsync(
            new CheckoutRequest(
                new[] { new CartLineRequest(productId, null, 12_000) },
                "CASH",
                AmountPaid: 100_000));

        var item = Assert.Single(transaction.Items, row => row.ItemType == TransactionItemType.PRODUCT);
        Assert.Equal(4_500, item.SellingPrice);
    }

    [Fact]
    public async Task Checkout_computes_tax_rounding_and_change()
    {
        using var database = new TestDatabase();
        await database.InitializeAsync();

        var (productId, _) = await CreateProductAsync(database);
        await OpenShiftAsync(database);

        var transaction = await database.GetRequiredService<ITransactionService>().CheckoutAsync(
            new CheckoutRequest(
                new[] { new CartLineRequest(productId, null, 2_000) },
                "CASH",
                AmountPaid: 20_000));

        // 2 Pcs x 5.000 = 10.000, PPN 11% eksklusif = 1.100, total 11.100
        Assert.Equal(10_000, transaction.ItemsAmount);
        Assert.Equal(1_100, transaction.TaxAmount);
        Assert.Equal(11_100, transaction.TotalNetAmount);
        Assert.Equal(8_900, transaction.AmountChange);
    }

    [Fact]
    public async Task Checkout_rejects_cash_below_the_total()
    {
        using var database = new TestDatabase();
        await database.InitializeAsync();

        var (productId, _) = await CreateProductAsync(database);
        await OpenShiftAsync(database);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            database.GetRequiredService<ITransactionService>().CheckoutAsync(new CheckoutRequest(
                new[] { new CartLineRequest(productId, null, 2_000) },
                "CASH",
                AmountPaid: 5_000)));
    }

    [Fact]
    public async Task Void_restores_stock_and_marks_the_transaction_voided()
    {
        using var database = new TestDatabase();
        await database.InitializeAsync();

        var (productId, _) = await CreateProductAsync(database);
        await OpenShiftAsync(database);

        var transactionService = database.GetRequiredService<ITransactionService>();

        var transaction = await transactionService.CheckoutAsync(new CheckoutRequest(
            new[] { new CartLineRequest(productId, null, 3_000) },
            "CASH",
            AmountPaid: 100_000));

        await transactionService.VoidAsync(new VoidTransactionRequest(transaction.Id, "Salah input"));

        var inventory = await database.GetRequiredService<IStockService>().GetInventoryAsync(productId);
        Assert.Equal(20_000, inventory.StockQuantity);

        using var context = database.CreateContext();
        var stored = await context.Transactions.SingleAsync(row => row.Id == transaction.Id);

        Assert.Equal(TransactionStatus.VOIDED, stored.Status);
        Assert.Equal("Salah input", stored.VoidReason);
        Assert.NotNull(stored.VoidedAt);
        Assert.True(await context.StockMutations.AnyAsync(row => row.MutationType == StockMutationType.SALE_VOID));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => transactionService.VoidAsync(new VoidTransactionRequest(transaction.Id, "lagi")));
    }

    [Fact]
    public async Task Invoice_numbers_follow_the_prefix_date_sequence_format()
    {
        using var database = new TestDatabase();
        await database.InitializeAsync();

        var (productId, _) = await CreateProductAsync(database);
        await OpenShiftAsync(database);

        var transactionService = database.GetRequiredService<ITransactionService>();

        var first = await transactionService.CheckoutAsync(new CheckoutRequest(
            new[] { new CartLineRequest(productId, null, 1_000) }, "CASH", AmountPaid: 50_000));

        var second = await transactionService.CheckoutAsync(new CheckoutRequest(
            new[] { new CartLineRequest(productId, null, 1_000) }, "CASH", AmountPaid: 50_000));

        Assert.Matches(@"^INV-\d{8}-\d{4}$", first.InvoiceNumber);
        Assert.Equal($"INV-{DateTime.UtcNow.AddMinutes(420):yyyyMMdd}-0001", first.InvoiceNumber);
        Assert.EndsWith("0002", second.InvoiceNumber);
    }

    [Fact]
    public async Task Discount_requires_the_discount_permission()
    {
        using var database = new TestDatabase();
        await database.InitializeAsync();

        var (productId, _) = await CreateProductAsync(database);

        await LoginAsCashierAsync(database);
        await OpenShiftAsync(database);

        var transactionService = database.GetRequiredService<ITransactionService>();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => transactionService.CheckoutAsync(
            new CheckoutRequest(
                new[] { new CartLineRequest(productId, null, 2_000, DiscountAmount: 1_000) },
                "CASH",
                AmountPaid: 50_000)));
    }

    [Fact]
    public async Task Service_fee_line_is_added_when_the_setting_is_enabled()
    {
        using var database = new TestDatabase();
        await database.InitializeAsync();

        var (productId, _) = await CreateProductAsync(database);
        await OpenShiftAsync(database);

        using (var context = database.CreateContext())
        {
            var settings = await context.Settings.SingleAsync();
            settings.ServiceFeeEnabled = true;
            await context.SaveChangesAsync();
        }

        var productService = database.GetRequiredService<IProductService>();
        await productService.UpdateAsync(productId, new UpdateProductRequest(
            "SKU-001", "Keripik", null, null, null, 2_500, true, true));

        var transaction = await database.GetRequiredService<ITransactionService>().CheckoutAsync(
            new CheckoutRequest(
                new[] { new CartLineRequest(productId, null, 2_000, ApplyServiceFee: true) },
                "CASH",
                AmountPaid: 50_000));

        Assert.Equal(2_500, transaction.ServiceAmount);
        Assert.Contains(transaction.Items, row => row.ItemType == TransactionItemType.SERVICE);
        Assert.Equal(12_500, transaction.TotalGrossAmount);
    }

    private static async Task OpenShiftAsync(TestDatabase database) =>
        await database.GetRequiredService<IShiftService>().OpenAsync(new OpenShiftRequest(100_000));

    private static async Task<(string ProductId, string UnitId)> CreateProductAsync(TestDatabase database)
    {
        var unitId = await database.SeedUnitAsync("Pcs");

        var product = await database.GetRequiredService<IProductService>().CreateAsync(
            new CreateProductRequest("SKU-001", "Keripik", null, unitId, null, null, null, true));

        var detail = await database.GetRequiredService<IProductService>().GetDetailAsync(product.Id);
        await database.GetRequiredService<IProductService>().SetUnitPriceAsync(detail.Units.First().Id, 5_000);

        await database.GetRequiredService<IStockService>().RecordAdjustmentAsync(
            new StockAdjustmentRequest(
                StockAdjustmentKind.Opening,
                new[] { new StockAdjustmentItemRequest(product.Id, null, 20_000, null) }));

        return (product.Id, unitId);
    }

    private static async Task LoginAsCashierAsync(TestDatabase database)
    {
        var userService = database.GetRequiredService<IUserManagementService>();

        var cashierRoleId = (await userService.GetRolesAsync())
            .Single(role => role.Name == PermissionCatalog.CashierRoleName).Id;

        await userService.CreateUserAsync(new CreateUserRequest(
            "Kasir Satu",
            "kasir1",
            "rahasia123",
            cashierRoleId,
            null));

        await database.GetRequiredService<IAuthenticationService>()
            .LoginAsync(new LoginRequest("kasir1", "rahasia123"));
    }
}
