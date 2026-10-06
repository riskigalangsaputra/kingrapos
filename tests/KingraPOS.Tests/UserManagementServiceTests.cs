using KingraPOS.Application.Abstractions.Services;
using KingraPOS.Application.Dtos;
using KingraPOS.Application.Security;
using KingraPOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace KingraPOS.Tests;

public class UserManagementServiceTests
{
    [Fact]
    public async Task CreateUser_requires_the_user_manage_permission()
    {
        using var database = new TestDatabase();
        await database.GetRequiredService<ISetupService>().CompleteAsync(TestData.CreateSetupRequest());

        var service = database.GetRequiredService<IUserManagementService>();
        var roles = await service.GetRolesAsync();
        var cashierRole = roles.Single(role => role.Name == PermissionCatalog.CashierRoleName);

        var request = new CreateUserRequest("Kasir Satu", "kasir1", "rahasia123", cashierRole.Id, null);

        // session belum login → tidak punya izin apa pun
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.CreateUserAsync(request));
    }

    [Fact]
    public async Task CreateUser_succeeds_for_an_authorized_user()
    {
        using var database = new TestDatabase();
        await database.GetRequiredService<ISetupService>().CompleteAsync(TestData.CreateSetupRequest());
        await database.GetRequiredService<IAuthenticationService>()
            .LoginAsync(new LoginRequest(TestData.OwnerUsername, TestData.OwnerPassword));

        var service = database.GetRequiredService<IUserManagementService>();
        var roles = await service.GetRolesAsync();
        var cashierRole = roles.Single(role => role.Name == PermissionCatalog.CashierRoleName);

        var created = await service.CreateUserAsync(
            new CreateUserRequest("Kasir Satu", "kasir1", "rahasia123", cashierRole.Id, "K-001"));

        Assert.Equal("kasir1", created.Username);
        Assert.Equal(PermissionCatalog.CashierRoleName, created.RoleName);

        var users = await service.GetUsersAsync();
        Assert.Equal(2, users.Count);
    }

    [Fact]
    public async Task CreateUser_rejects_a_duplicate_username()
    {
        using var database = new TestDatabase();
        await database.GetRequiredService<ISetupService>().CompleteAsync(TestData.CreateSetupRequest());
        await database.GetRequiredService<IAuthenticationService>()
            .LoginAsync(new LoginRequest(TestData.OwnerUsername, TestData.OwnerPassword));

        var service = database.GetRequiredService<IUserManagementService>();
        var roles = await service.GetRolesAsync();
        var cashierRole = roles.Single(role => role.Name == PermissionCatalog.CashierRoleName);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateUserAsync(
            new CreateUserRequest("Duplikat", TestData.OwnerUsername, "rahasia123", cashierRole.Id, null)));
    }

    [Fact]
    public async Task Cashier_can_login_after_being_created()
    {
        using var database = new TestDatabase();
        await database.GetRequiredService<ISetupService>().CompleteAsync(TestData.CreateSetupRequest());
        await database.GetRequiredService<IAuthenticationService>()
            .LoginAsync(new LoginRequest(TestData.OwnerUsername, TestData.OwnerPassword));

        var service = database.GetRequiredService<IUserManagementService>();
        var roles = await service.GetRolesAsync();
        var cashierRole = roles.Single(role => role.Name == PermissionCatalog.CashierRoleName);

        await service.CreateUserAsync(new CreateUserRequest("Kasir Satu", "kasir1", "rahasia123", cashierRole.Id, null));

        var login = await database.GetRequiredService<IAuthenticationService>()
            .LoginAsync(new LoginRequest("kasir1", "rahasia123"));

        Assert.True(login.Success);
        Assert.Equal(PermissionCatalog.CashierRoleName, login.User!.RoleName);

        var permissions = database.GetRequiredService<ICurrentUserSession>().Permissions;
        Assert.Contains(PermissionCatalog.SalesCreate, permissions);
        Assert.DoesNotContain(PermissionCatalog.ProductViewCostPrice, permissions);
    }

    [Fact]
    public async Task Deactivating_a_user_blocks_login()
    {
        using var database = new TestDatabase();
        await database.GetRequiredService<ISetupService>().CompleteAsync(TestData.CreateSetupRequest());
        await database.GetRequiredService<IAuthenticationService>()
            .LoginAsync(new LoginRequest(TestData.OwnerUsername, TestData.OwnerPassword));

        var service = database.GetRequiredService<IUserManagementService>();
        var roles = await service.GetRolesAsync();
        var cashierRole = roles.Single(role => role.Name == PermissionCatalog.CashierRoleName);

        var created = await service.CreateUserAsync(
            new CreateUserRequest("Kasir Satu", "kasir1", "rahasia123", cashierRole.Id, null));

        await service.SetUserActiveAsync(created.Id, false);

        var login = await database.GetRequiredService<IAuthenticationService>()
            .LoginAsync(new LoginRequest("kasir1", "rahasia123"));

        Assert.False(login.Success);
    }

    [Fact]
    public async Task Cannot_deactivate_the_signed_in_user()
    {
        using var database = new TestDatabase();
        await database.GetRequiredService<ISetupService>().CompleteAsync(TestData.CreateSetupRequest());

        var login = await database.GetRequiredService<IAuthenticationService>()
            .LoginAsync(new LoginRequest(TestData.OwnerUsername, TestData.OwnerPassword));

        var service = database.GetRequiredService<IUserManagementService>();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.SetUserActiveAsync(login.User!.UserId, false));
    }

    [Fact]
    public async Task Role_and_permission_tables_stay_consistent_after_adding_a_user()
    {
        using var database = new TestDatabase();
        await database.GetRequiredService<ISetupService>().CompleteAsync(TestData.CreateSetupRequest());
        await database.GetRequiredService<IAuthenticationService>()
            .LoginAsync(new LoginRequest(TestData.OwnerUsername, TestData.OwnerPassword));

        var service = database.GetRequiredService<IUserManagementService>();
        var roles = await service.GetRolesAsync();
        var adminRole = roles.Single(role => role.Name == PermissionCatalog.AdminRoleName);

        await service.CreateUserAsync(new CreateUserRequest("Admin Satu", "admin1", "rahasia123", adminRole.Id, null));

        var adminPermissions = await database.GetRequiredService<IPermissionService>()
            .GetRolePermissionsAsync(adminRole.Id);

        Assert.Contains(PermissionCatalog.SalesVoid, adminPermissions);
        Assert.DoesNotContain(PermissionCatalog.SettingsLicense, adminPermissions);
        Assert.DoesNotContain(PermissionCatalog.SettingsRestore, adminPermissions);
        Assert.DoesNotContain(PermissionCatalog.RoleManage, adminPermissions);
    }

    [Fact]
    public async Task Setup_creates_exactly_three_system_roles()
    {
        using var database = new TestDatabase();
        await database.GetRequiredService<ISetupService>().CompleteAsync(TestData.CreateSetupRequest());

        using var context = database.CreateContext();
        var roles = await context.Roles.ToListAsync();

        Assert.Equal(3, roles.Count);
        Assert.All(roles, role => Assert.True(role.IsSystem));
        Assert.Equal(
            new[] { PermissionCatalog.AdminRoleName, PermissionCatalog.CashierRoleName, PermissionCatalog.OwnerRoleName },
            roles.Select(role => role.Name).OrderBy(name => name, StringComparer.Ordinal));

        var ownerRoleId = roles.Single(role => role.Name == PermissionCatalog.OwnerRoleName).Id;

        Assert.Equal(1, await context.Users.CountAsync(user => user.RoleId == ownerRoleId));
        Assert.Equal(0, await context.UserPermissions.CountAsync());
        Assert.True(await context.RolePermissions.CountAsync() > 0);
    }

    [Fact]
    public async Task Seed_schema_creates_app_meta_and_settings_rows()
    {
        using var database = new TestDatabase();
        using var context = database.CreateContext();

        Assert.Equal(1, await context.Settings.CountAsync());
        Assert.True(await context.AppMetaEntries.CountAsync() >= 2);
        Assert.Empty(await context.Users.ToListAsync());
    }
}
