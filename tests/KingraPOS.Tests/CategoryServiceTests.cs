using KingraPOS.Application.Abstractions.Services;
using KingraPOS.Application.Dtos;
using KingraPOS.Application.Security;

namespace KingraPOS.Tests;

public class CategoryServiceTests
{
    [Fact]
    public async Task Create_then_list_returns_the_category()
    {
        using var database = new TestDatabase();
        await database.InitializeAsync();

        var service = database.GetRequiredService<ICategoryService>();
        var created = await service.CreateAsync(new CategoryRequest("Makanan"));

        Assert.Equal("Makanan", created.Name);

        var all = await service.GetAllAsync();
        Assert.Single(all);
        Assert.Equal(created.Id, all[0].Id);
    }

    [Fact]
    public async Task Create_requires_the_category_manage_permission()
    {
        using var database = new TestDatabase();
        await database.GetRequiredService<ISetupService>().CompleteAsync(TestData.CreateSetupRequest());

        var service = database.GetRequiredService<ICategoryService>();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.CreateAsync(new CategoryRequest("Makanan")));
    }

    [Fact]
    public async Task Duplicate_active_name_is_rejected()
    {
        using var database = new TestDatabase();
        await database.InitializeAsync();

        var service = database.GetRequiredService<ICategoryService>();
        await service.CreateAsync(new CategoryRequest("Minuman"));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateAsync(new CategoryRequest("  Minuman  ")));
    }

    [Fact]
    public async Task Renaming_to_an_existing_name_is_rejected()
    {
        using var database = new TestDatabase();
        await database.InitializeAsync();

        var service = database.GetRequiredService<ICategoryService>();
        var first = await service.CreateAsync(new CategoryRequest("Minuman"));
        await service.CreateAsync(new CategoryRequest("Makanan"));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.UpdateAsync(first.Id, new CategoryRequest("Makanan")));
    }

    [Fact]
    public async Task Delete_is_blocked_while_a_product_uses_the_category()
    {
        using var database = new TestDatabase();
        await database.InitializeAsync();

        var service = database.GetRequiredService<ICategoryService>();
        var category = await service.CreateAsync(new CategoryRequest("Minuman"));

        var unitId = await database.SeedUnitAsync("Pcs");
        await database.SeedProductAsync("Teh", "SKU-1", unitId, category.Id);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeleteAsync(category.Id));
    }

    [Fact]
    public async Task Deleted_name_can_be_reused()
    {
        using var database = new TestDatabase();
        await database.InitializeAsync();

        var service = database.GetRequiredService<ICategoryService>();
        var category = await service.CreateAsync(new CategoryRequest("Sementara"));

        await service.DeleteAsync(category.Id);

        Assert.Empty(await service.GetAllAsync());

        var recreated = await service.CreateAsync(new CategoryRequest("Sementara"));
        Assert.Equal("Sementara", recreated.Name);
    }

    [Fact]
    public async Task Deletion_requires_write_access_from_the_license()
    {
        using var database = new TestDatabase();
        await database.InitializeAsync();

        var service = database.GetRequiredService<ICategoryService>();
        var category = await service.CreateAsync(new CategoryRequest("X"));

        await SetLicenseExpiredAsync(database);

        await Assert.ThrowsAsync<LicenseRestrictedException>(() => service.DeleteAsync(category.Id));
    }

    private static async Task SetLicenseExpiredAsync(TestDatabase database)
    {
        using var context = database.CreateContext();

        var license = context.AppLicenses.Single();
        license.ExpiresAt = DateTimeOffset.UtcNow.AddDays(-90);

        await context.SaveChangesAsync();
    }
}
