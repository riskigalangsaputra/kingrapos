using KingraPOS.Application.Abstractions.Services;
using KingraPOS.Application.Dtos;
using KingraPOS.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace KingraPOS.Tests;

public class ShiftServiceTests
{
    [Fact]
    public async Task Opening_a_shift_stores_the_starting_cash_and_status()
    {
        using var database = new TestDatabase();
        await database.InitializeAsync();

        var shiftService = database.GetRequiredService<IShiftService>();

        var shift = await shiftService.OpenAsync(new OpenShiftRequest(150_000, "Shift pagi"));

        Assert.Equal(150_000, shift.CashDrawerStart);
        Assert.Equal(ShiftStatus.OPEN, shift.Status);
        Assert.Null(shift.EndTime);

        Assert.NotNull(await shiftService.GetOpenShiftAsync());
    }

    [Fact]
    public async Task Only_one_shift_may_be_open_at_a_time()
    {
        using var database = new TestDatabase();
        await database.InitializeAsync();

        var shiftService = database.GetRequiredService<IShiftService>();
        await shiftService.OpenAsync(new OpenShiftRequest(100_000));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => shiftService.OpenAsync(new OpenShiftRequest(50_000)));
    }

    [Fact]
    public async Task Closing_a_shift_records_the_discrepancy_against_system_cash()
    {
        using var database = new TestDatabase();
        await database.InitializeAsync();

        var shiftService = database.GetRequiredService<IShiftService>();
        var shift = await shiftService.OpenAsync(new OpenShiftRequest(100_000));

        var closed = await shiftService.CloseAsync(new CloseShiftRequest(shift.Id, 90_000));

        Assert.Equal(ShiftStatus.CLOSED, closed.Status);
        Assert.Equal(100_000, closed.CashDrawerEndSystem);
        Assert.Equal(90_000, closed.CashDrawerEndPhysical);
        Assert.Equal(-10_000, closed.DiscrepancyAmount);
        Assert.NotNull(closed.EndTime);

        Assert.Null(await shiftService.GetOpenShiftAsync());
    }

    [Fact]
    public async Task Closing_an_already_closed_shift_is_rejected()
    {
        using var database = new TestDatabase();
        await database.InitializeAsync();

        var shiftService = database.GetRequiredService<IShiftService>();
        var shift = await shiftService.OpenAsync(new OpenShiftRequest(100_000));

        await shiftService.CloseAsync(new CloseShiftRequest(shift.Id, 100_000));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => shiftService.CloseAsync(new CloseShiftRequest(shift.Id, 100_000)));
    }

    [Fact]
    public async Task Cash_sales_are_included_in_the_expected_drawer_amount()
    {
        using var database = new TestDatabase();
        await database.InitializeAsync();

        var (productId, _) = await CreateProductAsync(database);

        var shiftService = database.GetRequiredService<IShiftService>();
        await shiftService.OpenAsync(new OpenShiftRequest(100_000));

        var sale = await database.GetRequiredService<ITransactionService>().CheckoutAsync(
            new CheckoutRequest(
                new[] { new CartLineRequest(productId, null, 2_000) },
                "CASH",
                AmountPaid: 100_000));

        var current = await shiftService.GetOpenShiftAsync();

        Assert.NotNull(current);
        Assert.Equal(sale.TotalNetAmount, current!.CashSalesAmount);
        Assert.Equal(1, current.SalesCount);
        Assert.Equal(100_000 + sale.TotalNetAmount, current.CashDrawerStart + current.CashSalesAmount);
    }

    private static async Task<(string ProductId, string UnitId)> CreateProductAsync(TestDatabase database)
    {
        var unitId = await database.SeedUnitAsync("Pcs");

        var product = await database.GetRequiredService<IProductService>().CreateAsync(
            new CreateProductRequest("SKU-001", "Keripik", null, unitId, null, null, null, true));

        var detail = await database.GetRequiredService<IProductService>().GetDetailAsync(product.Id);
        await database.GetRequiredService<IProductService>().SetUnitPriceAsync(detail.Units.First().Id, 5_000);

        await database.GetRequiredService<IStockService>().RecordAdjustmentAsync(
            new StockAdjustmentRequest(
                KingraPOS.Application.Stock.StockAdjustmentKind.Opening,
                new[] { new StockAdjustmentItemRequest(product.Id, null, 20_000, null) }));

        return (product.Id, unitId);
    }
}
