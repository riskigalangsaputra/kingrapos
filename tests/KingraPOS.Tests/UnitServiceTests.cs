using KingraPOS.Application.Abstractions.Services;
using KingraPOS.Application.Dtos;

namespace KingraPOS.Tests;

public class UnitServiceTests
{
    [Fact]
    public async Task Create_persists_allow_decimal()
    {
        using var database = new TestDatabase();
        await database.InitializeAsync();

        var service = database.GetRequiredService<IUnitService>();

        var pcs = await service.CreateAsync(new UnitRequest("Pcs", null, AllowDecimal: false));
        var kilogram = await service.CreateAsync(new UnitRequest("Kg", "Satuan berat", AllowDecimal: true));

        Assert.False(pcs.AllowDecimal);
        Assert.True(kilogram.AllowDecimal);

        var all = await service.GetAllAsync();
        Assert.Equal(2, all.Count);
        Assert.Equal("Kg", all[0].Name);
    }

    [Fact]
    public async Task Duplicate_name_is_rejected()
    {
        using var database = new TestDatabase();
        await database.InitializeAsync();

        var service = database.GetRequiredService<IUnitService>();
        await service.CreateAsync(new UnitRequest("Pcs", null, false));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateAsync(new UnitRequest("Pcs", null, false)));
    }

    [Fact]
    public async Task Delete_is_blocked_while_a_product_uses_the_unit()
    {
        using var database = new TestDatabase();
        await database.InitializeAsync();

        var service = database.GetRequiredService<IUnitService>();
        var unit = await service.CreateAsync(new UnitRequest("Dus", null, false));

        await database.SeedProductAsync("Keripik", "SKU-2", unit.Id);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeleteAsync(unit.Id));
    }

    [Fact]
    public async Task Update_changes_name_and_flags()
    {
        using var database = new TestDatabase();
        await database.InitializeAsync();

        var service = database.GetRequiredService<IUnitService>();
        var unit = await service.CreateAsync(new UnitRequest("Liter", null, false));

        var updated = await service.UpdateAsync(unit.Id, new UnitRequest("Litre", "Satuan volume", true));

        Assert.Equal("Litre", updated.Name);
        Assert.Equal("Satuan volume", updated.Description);
        Assert.True(updated.AllowDecimal);
    }

    [Fact]
    public async Task Deleted_name_can_be_reused()
    {
        using var database = new TestDatabase();
        await database.InitializeAsync();

        var service = database.GetRequiredService<IUnitService>();
        var unit = await service.CreateAsync(new UnitRequest("Sementara", null, false));

        await service.DeleteAsync(unit.Id);

        var recreated = await service.CreateAsync(new UnitRequest("Sementara", null, false));
        Assert.NotEqual(unit.Id, recreated.Id);
    }
}
