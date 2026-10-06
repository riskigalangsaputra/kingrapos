using KingraPOS.Application.Abstractions.Services;
using KingraPOS.Application.Security;
using KingraPOS.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace KingraPOS.Tests;

public class SetupServiceTests
{
    [Fact]
    public async Task IsCompleted_is_false_before_setup()
    {
        using var database = new TestDatabase();
        var setupService = database.GetRequiredService<ISetupService>();

        Assert.False(await setupService.IsCompletedAsync());
    }

    [Fact]
    public async Task Complete_creates_profile_roles_owner_and_license()
    {
        using var database = new TestDatabase();
        var setupService = database.GetRequiredService<ISetupService>();

        await setupService.CompleteAsync(TestData.CreateSetupRequest());

        Assert.True(await setupService.IsCompletedAsync());

        using var context = database.CreateContext();

        Assert.Equal(1, await context.BusinessProfiles.CountAsync());
        Assert.Equal(3, await context.Roles.CountAsync());
        Assert.Equal(1, await context.Users.CountAsync());
        Assert.Equal(1, await context.AppLicenses.CountAsync());

        var profile = await context.BusinessProfiles.SingleAsync();
        Assert.Equal("Toko Uji", profile.Name);

        var owner = await context.Users.SingleAsync();
        Assert.Equal(TestData.OwnerUsername, owner.Username);
        Assert.True(owner.IsActive);
        Assert.NotEqual(TestData.OwnerPassword, owner.PasswordHash);

        var license = await context.AppLicenses.SingleAsync();
        Assert.Equal(LicenseStatus.TRIAL, license.Status);
        Assert.Equal("OFFLINE", license.Edition);
    }

    [Fact]
    public async Task Complete_applies_settings_from_the_request()
    {
        using var database = new TestDatabase();
        await database.GetRequiredService<ISetupService>().CompleteAsync(TestData.CreateSetupRequest());

        using var context = database.CreateContext();
        var settings = await context.Settings.SingleAsync();

        Assert.True(settings.TaxEnabled);
        Assert.Equal(11.0, settings.TaxRate);
        Assert.False(settings.TaxInclusive);
        Assert.Equal(100, settings.CashRounding);
        Assert.Equal("INV", settings.InvoicePrefix);
        Assert.Equal(420, settings.UtcOffsetMinutes);
    }

    [Fact]
    public async Task Permission_catalog_matches_the_seeded_permissions()
    {
        using var database = new TestDatabase();
        using var context = database.CreateContext();

        var seeded = await context.Permissions
            .Select(permission => permission.PermissionKey)
            .ToListAsync();

        Assert.Equal(
            seeded.OrderBy(key => key, StringComparer.Ordinal),
            PermissionCatalog.All.OrderBy(key => key, StringComparer.Ordinal));
    }

    [Fact]
    public async Task Owner_role_receives_every_permission_and_cashier_does_not()
    {
        using var database = new TestDatabase();
        await database.GetRequiredService<ISetupService>().CompleteAsync(TestData.CreateSetupRequest());

        using var context = database.CreateContext();

        var ownerRole = await context.Roles.SingleAsync(role => role.Name == PermissionCatalog.OwnerRoleName);
        var cashierRole = await context.Roles.SingleAsync(role => role.Name == PermissionCatalog.CashierRoleName);

        var ownerPermissions = await context.RolePermissions
            .Where(permission => permission.RoleId == ownerRole.Id)
            .Select(permission => permission.PermissionKey)
            .ToListAsync();

        var cashierPermissions = await context.RolePermissions
            .Where(permission => permission.RoleId == cashierRole.Id)
            .Select(permission => permission.PermissionKey)
            .ToListAsync();

        Assert.Equal(PermissionCatalog.All.Count, ownerPermissions.Count);
        Assert.DoesNotContain(PermissionCatalog.ProductViewCostPrice, cashierPermissions);
        Assert.DoesNotContain(PermissionCatalog.SalesVoid, cashierPermissions);
        Assert.DoesNotContain(PermissionCatalog.SettingsBackup, cashierPermissions);
        Assert.Contains(PermissionCatalog.SalesCreate, cashierPermissions);
    }

    [Fact]
    public async Task Complete_is_rejected_when_setup_already_ran()
    {
        using var database = new TestDatabase();
        var setupService = database.GetRequiredService<ISetupService>();

        await setupService.CompleteAsync(TestData.CreateSetupRequest());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => setupService.CompleteAsync(TestData.CreateSetupRequest()));
    }

    [Fact]
    public async Task Complete_writes_an_activity_log_entry()
    {
        using var database = new TestDatabase();
        await database.GetRequiredService<ISetupService>().CompleteAsync(TestData.CreateSetupRequest());

        using var context = database.CreateContext();

        Assert.Contains(
            await context.ActivityLogs.Select(log => log.ActionType).ToListAsync(),
            action => action == "SETUP_COMPLETED");
    }
}
