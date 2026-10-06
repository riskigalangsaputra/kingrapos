using KingraPOS.Application.Abstractions.Services;
using KingraPOS.Application.Dtos;
using KingraPOS.Application.Security;
using KingraPOS.Domain.Entities;
using KingraPOS.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace KingraPOS.Tests;

public class ProductServiceTests
{
    [Fact]
    public async Task Create_builds_the_base_unit_price_and_inventory_row()
    {
        using var database = new TestDatabase();
        await database.InitializeAsync();

        var (productId, unitId) = await CreateProductAsync(database);

        var detail = await database.GetRequiredService<IProductService>().GetDetailAsync(productId);

        Assert.Equal("SKU-001", detail.Sku);
        Assert.Equal("Keripik", detail.Name);
        Assert.True(detail.IsNewProduct);
        Assert.True(detail.IsActive);

        var unit = Assert.Single(detail.Units);
        Assert.Equal(unitId, unit.UnitId);
        Assert.True(unit.IsBaseUnit);
        Assert.Equal(1000, unit.ConversionFactor);
        Assert.Equal(0, unit.SellingPrice);
        Assert.Empty(unit.Tiers);

        using var context = database.CreateContext();
        Assert.Equal(1, await context.Inventories.CountAsync());
    }

    [Fact]
    public async Task Duplicate_sku_is_rejected()
    {
        using var database = new TestDatabase();
        await database.InitializeAsync();

        var (_, unitId) = await CreateProductAsync(database);

        await Assert.ThrowsAsync<InvalidOperationException>(() => database
            .GetRequiredService<IProductService>()
            .CreateAsync(new CreateProductRequest("SKU-001", "Lain", null, unitId, null, null, null, true)));
    }

    [Fact]
    public async Task Inactive_sku_may_be_reused_after_the_product_is_deleted()
    {
        using var database = new TestDatabase();
        await database.InitializeAsync();

        var (productId, unitId) = await CreateProductAsync(database);

        await database.GetRequiredService<IProductService>().DeleteAsync(productId);

        var recreated = await database.GetRequiredService<IProductService>()
            .CreateAsync(new CreateProductRequest("SKU-001", "Keripik Baru", null, unitId, null, null, null, true));

        Assert.Equal("SKU-001", recreated.Sku);
    }

    [Fact]
    public async Task Adding_a_selling_unit_requires_unique_barcode_and_distinct_unit()
    {
        using var database = new TestDatabase();
        await database.InitializeAsync();

        var (productId, baseUnitId) = await CreateProductAsync(database);
        var dusId = await database.SeedUnitAsync("Dus");

        var service = database.GetRequiredService<IProductService>();

        var added = await service.AddUnitAsync(productId, new ProductUnitRequest(dusId, "BC-1", 12_000));

        Assert.False(added.IsBaseUnit);
        Assert.Equal(12_000, added.ConversionFactor);
        Assert.Equal("BC-1", added.Barcode);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.AddUnitAsync(productId, new ProductUnitRequest(dusId, null, 12_000)));

        var otherUnitId = await database.SeedUnitAsync("Bal");
        var otherProduct = await service.CreateAsync(new CreateProductRequest(
            "SKU-002", "Produk Lain", null, baseUnitId, null, null, null, true));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.AddUnitAsync(otherProduct.Id, new ProductUnitRequest(otherUnitId, "BC-1", 24_000)));
    }

    [Fact]
    public async Task Base_unit_cannot_be_removed_and_its_conversion_stays_fixed()
    {
        using var database = new TestDatabase();
        await database.InitializeAsync();

        var (productId, _) = await CreateProductAsync(database);
        var service = database.GetRequiredService<IProductService>();

        var baseUnit = (await service.GetDetailAsync(productId)).Units.Single();

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RemoveUnitAsync(baseUnit.Id));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.UpdateUnitAsync(baseUnit.Id, new ProductUnitRequest(
                baseUnit.UnitId, null, 12_000)));
    }

    [Fact]
    public async Task Selling_unit_can_be_removed_when_unused()
    {
        using var database = new TestDatabase();
        await database.InitializeAsync();

        var (productId, _) = await CreateProductAsync(database);
        var dusId = await database.SeedUnitAsync("Dus");

        var service = database.GetRequiredService<IProductService>();
        var added = await service.AddUnitAsync(productId, new ProductUnitRequest(dusId, null, 12_000));

        await service.RemoveUnitAsync(added.Id);

        Assert.Single((await service.GetDetailAsync(productId)).Units);
    }

    [Fact]
    public async Task Setting_a_price_records_history_and_updates_the_trend_cache()
    {
        using var database = new TestDatabase();
        await database.InitializeAsync();

        var (productId, _) = await CreateProductAsync(database);
        var service = database.GetRequiredService<IProductService>();
        var unitId = (await service.GetDetailAsync(productId)).Units.Single().Id;

        await service.SetUnitPriceAsync(unitId, 10_000);
        await service.SetUnitPriceAsync(unitId, 12_500);

        var unit = (await service.GetDetailAsync(productId)).Units.Single();

        Assert.Equal(12_500, unit.SellingPrice);
        Assert.Equal(10_000, unit.PreviousSellingPrice);
        Assert.Equal(PriceTrend.UP, unit.PriceTrend);
        Assert.NotNull(unit.PriceChangedAt);
        Assert.True(unit.ShowTrendBadge);

        var history = await service.GetPriceHistoryAsync(productId);
        Assert.Equal(2, history.Count);
        Assert.All(history, entry => Assert.Equal(PriceChangeType.SELLING, entry.PriceType));
    }

    [Fact]
    public async Task Setting_the_same_price_twice_records_nothing()
    {
        using var database = new TestDatabase();
        await database.InitializeAsync();

        var (productId, _) = await CreateProductAsync(database);
        var service = database.GetRequiredService<IProductService>();
        var unitId = (await service.GetDetailAsync(productId)).Units.Single().Id;

        await service.SetUnitPriceAsync(unitId, 10_000);
        await service.SetUnitPriceAsync(unitId, 10_000);

        Assert.Single(await service.GetPriceHistoryAsync(productId));
    }

    [Fact]
    public async Task Tiers_are_replaced_and_drive_the_resolved_price()
    {
        using var database = new TestDatabase();
        await database.InitializeAsync();

        var (productId, _) = await CreateProductAsync(database);
        var service = database.GetRequiredService<IProductService>();
        var unitId = (await service.GetDetailAsync(productId)).Units.Single().Id;

        await service.SetUnitPriceAsync(unitId, 10_000);
        await service.SetTiersAsync(unitId, new[]
        {
            new ProductPriceTierRequest("Grosir", 10_000, 9_000),
            new ProductPriceTierRequest("Bal", 100_000, 8_000)
        });

        Assert.Equal(10_000, await service.ResolvePriceAsync(unitId, 1_000));
        Assert.Equal(9_000, await service.ResolvePriceAsync(unitId, 10_000));
        Assert.Equal(9_000, await service.ResolvePriceAsync(unitId, 50_000));
        Assert.Equal(8_000, await service.ResolvePriceAsync(unitId, 100_000));

        await service.SetTiersAsync(unitId, new[] { new ProductPriceTierRequest("Grosir", 10_000, 8_500) });

        var tiers = (await service.GetDetailAsync(productId)).Units.Single().Tiers;

        var tier = Assert.Single(tiers);
        Assert.Equal(8_500, tier.TierPrice);
        Assert.Equal(8_500, await service.ResolvePriceAsync(unitId, 20_000));
    }

    [Fact]
    public async Task Cost_price_is_hidden_from_users_without_the_permission()
    {
        using var database = new TestDatabase();
        await database.InitializeAsync();

        var (productId, unitId) = await CreateProductAsync(database, baseCostPrice: 7_500);

        // owner punya izin
        var ownerView = await database.GetRequiredService<IProductService>().GetDetailAsync(productId);
        Assert.Equal(7_500, ownerView.BaseCostPrice);

        await LoginAsCashierAsync(database);

        var cashierService = database.GetRequiredService<IProductService>();
        var cashierView = await cashierService.GetDetailAsync(productId);

        Assert.Null(cashierView.BaseCostPrice);

        var summary = (await cashierService.SearchAsync("Keripik")).Single();
        Assert.Null(summary.BaseCostPrice);

        // input HPP diabaikan bila tidak berizin
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => cashierService.UpdateAsync(
            productId,
            new UpdateProductRequest("SKU-001", "Keripik", null, null, 99_000, null, true, true)));

        Assert.Equal(unitId, cashierView.BaseUnitId);
    }

    [Fact]
    public async Task Updating_the_cost_price_requires_the_permission()
    {
        using var database = new TestDatabase();
        await database.InitializeAsync();

        var (productId, _) = await CreateProductAsync(database);

        var updated = await database.GetRequiredService<IProductService>().UpdateAsync(
            productId,
            new UpdateProductRequest("SKU-001", "Keripik", null, null, 8_000, null, true, true));

        Assert.Equal(8_000, updated.BaseCostPrice);
    }

    [Fact]
    public async Task Delete_is_blocked_once_stock_history_exists()
    {
        using var database = new TestDatabase();
        await database.InitializeAsync();

        var (productId, _) = await CreateProductAsync(database);

        using (var context = database.CreateContext())
        {
            var userId = context.Users.Single().Id;

            context.StockMutations.Add(new StockMutation
            {
                ProductId = productId,
                UserId = userId,
                MutationType = StockMutationType.SALE,
                StockBucket = StockBucket.AVAILABLE,
                Quantity = -1000,
                CreatedAt = DateTimeOffset.UtcNow
            });

            await context.SaveChangesAsync();
        }

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => database.GetRequiredService<IProductService>().DeleteAsync(productId));
    }

    [Fact]
    public async Task Search_matches_name_sku_and_barcode()
    {
        using var database = new TestDatabase();
        await database.InitializeAsync();

        var (productId, _) = await CreateProductAsync(database);
        var dusId = await database.SeedUnitAsync("Dus");

        var service = database.GetRequiredService<IProductService>();
        await service.AddUnitAsync(productId, new ProductUnitRequest(dusId, "BC-999", 12_000));

        Assert.Single(await service.SearchAsync("keripik"));
        Assert.Single(await service.SearchAsync("SKU-001"));
        Assert.Single(await service.SearchAsync("BC-999"));
        Assert.Empty(await service.SearchAsync("tidak-ada"));
    }

    [Fact]
    public async Task New_product_badge_is_cleared_when_the_badge_window_is_disabled()
    {
        using var database = new TestDatabase();
        await database.InitializeAsync();

        var (productId, _) = await CreateProductAsync(database);
        var service = database.GetRequiredService<IProductService>();

        Assert.True((await service.GetDetailAsync(productId)).IsNewProduct);

        using (var context = database.CreateContext())
        {
            var settings = await context.Settings.SingleAsync();
            settings.NewProductBadgeDays = 0;
            await context.SaveChangesAsync();
        }

        Assert.Equal(1, await service.RefreshNewProductBadgesAsync());
        Assert.False((await service.GetDetailAsync(productId)).IsNewProduct);

        // sudah bersih, tidak ada lagi yang diubah
        Assert.Equal(0, await service.RefreshNewProductBadgesAsync());
    }

    private static async Task<(string ProductId, string UnitId)> CreateProductAsync(
        TestDatabase database,
        long? baseCostPrice = null)
    {
        var unitId = await database.SeedUnitAsync("Pcs");

        var created = await database.GetRequiredService<IProductService>().CreateAsync(
            new CreateProductRequest(
                "SKU-001",
                "Keripik",
                null,
                unitId,
                "Keripik kentang",
                baseCostPrice,
                null,
                true));

        return (created.Id, unitId);
    }

    private static async Task LoginAsCashierAsync(TestDatabase database)
    {
        var userService = database.GetRequiredService<IUserManagementService>();
        var roles = await userService.GetRolesAsync();
        var cashierRoleId = roles.Single(role => role.Name == PermissionCatalog.CashierRoleName).Id;

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
