using KingraPOS.Application.Abstractions.Services;
using KingraPOS.Application.Dtos;
using KingraPOS.Application.Security;
using Microsoft.EntityFrameworkCore;

namespace KingraPOS.Tests;

public class RoleManagementServiceTests
{
    [Fact]
    public async Task SetRolePermissions_requires_the_role_manage_permission()
    {
        using var database = new TestDatabase();
        await database.GetRequiredService<ISetupService>().CompleteAsync(TestData.CreateSetupRequest());

        var service = database.GetRequiredService<IRoleManagementService>();
        var roles = await service.GetRolesAsync();
        var cashier = roles.Single(role => role.Name == PermissionCatalog.CashierRoleName);

        // belum login → tidak punya izin
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.SetRolePermissionsAsync(cashier.Id, new[] { PermissionCatalog.SalesCreate }));
    }

    [Fact]
    public async Task SetRolePermissions_replaces_the_whole_matrix()
    {
        using var database = new TestDatabase();
        await SetupAndLoginAsync(database);

        var service = database.GetRequiredService<IRoleManagementService>();
        var cashier = (await service.GetRolesAsync()).Single(role => role.Name == PermissionCatalog.CashierRoleName);

        await service.SetRolePermissionsAsync(
            cashier.Id,
            new[] { PermissionCatalog.SalesCreate, PermissionCatalog.StockView });

        var keys = await service.GetRolePermissionKeysAsync(cashier.Id);

        Assert.Equal(2, keys.Count);
        Assert.Contains(PermissionCatalog.SalesCreate, keys);
        Assert.Contains(PermissionCatalog.StockView, keys);
        Assert.DoesNotContain(PermissionCatalog.SalesViewHistory, keys);

        using var context = database.CreateContext();
        Assert.Equal(2, await context.RolePermissions.CountAsync(row => row.RoleId == cashier.Id));
    }

    [Fact]
    public async Task Removing_a_role_permission_changes_effective_permissions()
    {
        using var database = new TestDatabase();
        await SetupAndLoginAsync(database);

        var service = database.GetRequiredService<IRoleManagementService>();
        var permissionService = database.GetRequiredService<IPermissionService>();
        var cashier = (await service.GetRolesAsync()).Single(role => role.Name == PermissionCatalog.CashierRoleName);

        var owner = (await database.GetRequiredService<IUserManagementService>().GetUsersAsync())
            .Single(user => user.Username == TestData.OwnerUsername);

        await service.SetRolePermissionsAsync(cashier.Id, new[] { PermissionCatalog.SalesCreate });

        var created = await database.GetRequiredService<IUserManagementService>().CreateUserAsync(
            new CreateUserRequest("Kasir Satu", "kasir1", "rahasia123", cashier.Id, null));

        var effective = await permissionService.GetEffectivePermissionsAsync(created.Id);

        Assert.Contains(PermissionCatalog.SalesCreate, effective);
        Assert.DoesNotContain(PermissionCatalog.StockView, effective);
        Assert.NotEmpty(owner.Username);
    }

    [Fact]
    public async Task User_override_allow_adds_a_permission_on_top_of_the_role()
    {
        using var database = new TestDatabase();
        await SetupAndLoginAsync(database);

        var service = database.GetRequiredService<IRoleManagementService>();
        var schema = await service.GetPermissionsAsync();
        var cashier = (await service.GetRolesAsync()).Single(role => role.Name == PermissionCatalog.CashierRoleName);

        var created = await database.GetRequiredService<IUserManagementService>().CreateUserAsync(
            new CreateUserRequest("Kasir Satu", "kasir1", "rahasia123", cashier.Id, null));

        Assert.DoesNotContain(PermissionCatalog.ProductViewCostPrice, await database
            .GetRequiredService<IPermissionService>()
            .GetEffectivePermissionsAsync(created.Id));

        await service.SetUserOverrideAsync(
            created.Id,
            PermissionCatalog.ProductViewCostPrice,
            PermissionOverrideState.Allow);

        var effective = await database.GetRequiredService<IPermissionService>()
            .GetEffectivePermissionsAsync(created.Id);

        Assert.Contains(PermissionCatalog.ProductViewCostPrice, effective);
        Assert.NotEmpty(schema);

        var overrides = await service.GetUserOverridesAsync(created.Id);
        Assert.Single(overrides);
        Assert.Equal(PermissionOverrideState.Allow, overrides[0].State);
    }

    [Fact]
    public async Task User_override_deny_removes_a_role_permission()
    {
        using var database = new TestDatabase();
        await SetupAndLoginAsync(database);

        var service = database.GetRequiredService<IRoleManagementService>();
        var cashier = (await service.GetRolesAsync()).Single(role => role.Name == PermissionCatalog.CashierRoleName);

        var created = await database.GetRequiredService<IUserManagementService>().CreateUserAsync(
            new CreateUserRequest("Kasir Satu", "kasir1", "rahasia123", cashier.Id, null));

        await service.SetUserOverrideAsync(
            created.Id,
            PermissionCatalog.SalesCreate,
            PermissionOverrideState.Deny);

        var effective = await database.GetRequiredService<IPermissionService>()
            .GetEffectivePermissionsAsync(created.Id);

        Assert.DoesNotContain(PermissionCatalog.SalesCreate, effective);

        var overrides = await service.GetUserOverridesAsync(created.Id);
        Assert.Equal(PermissionOverrideState.Deny, overrides.Single().State);
    }

    [Fact]
    public async Task Default_override_clears_the_exception()
    {
        using var database = new TestDatabase();
        await SetupAndLoginAsync(database);

        var service = database.GetRequiredService<IRoleManagementService>();
        var cashier = (await service.GetRolesAsync()).Single(role => role.Name == PermissionCatalog.CashierRoleName);

        var created = await database.GetRequiredService<IUserManagementService>().CreateUserAsync(
            new CreateUserRequest("Kasir Satu", "kasir1", "rahasia123", cashier.Id, null));

        await service.SetUserOverrideAsync(created.Id, PermissionCatalog.SalesCreate, PermissionOverrideState.Deny);
        await service.SetUserOverrideAsync(created.Id, PermissionCatalog.SalesCreate, PermissionOverrideState.Default);

        Assert.Empty(await service.GetUserOverridesAsync(created.Id));

        var effective = await database.GetRequiredService<IPermissionService>()
            .GetEffectivePermissionsAsync(created.Id);

        Assert.Contains(PermissionCatalog.SalesCreate, effective);

        using var context = database.CreateContext();
        Assert.Equal(0, await context.UserPermissions.CountAsync(row => row.UserId == created.Id));
    }

    [Fact]
    public async Task Role_and_override_changes_are_audited()
    {
        using var database = new TestDatabase();
        await SetupAndLoginAsync(database);

        var service = database.GetRequiredService<IRoleManagementService>();
        var cashier = (await service.GetRolesAsync()).Single(role => role.Name == PermissionCatalog.CashierRoleName);

        await service.SetRolePermissionsAsync(cashier.Id, new[] { PermissionCatalog.SalesCreate });

        using var context = database.CreateContext();

        Assert.Equal(1, await context.ActivityLogs
            .CountAsync(log => log.ActionType == "ROLE_PERMISSIONS_UPDATED"));
    }

    private static async Task SetupAndLoginAsync(TestDatabase database)
    {
        await database.GetRequiredService<ISetupService>().CompleteAsync(TestData.CreateSetupRequest());
        await database.GetRequiredService<IAuthenticationService>()
            .LoginAsync(new LoginRequest(TestData.OwnerUsername, TestData.OwnerPassword));
    }
}
