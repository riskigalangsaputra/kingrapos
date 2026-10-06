using KingraPOS.Application.Abstractions.Services;
using KingraPOS.Application.Security;
using KingraPOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace KingraPOS.Tests;

public class PermissionServiceTests
{
    [Fact]
    public async Task Owner_receives_all_permissions_from_the_role()
    {
        using var database = new TestDatabase();
        await database.GetRequiredService<ISetupService>().CompleteAsync(TestData.CreateSetupRequest());

        string userId;
        using (var context = database.CreateContext())
            userId = (await context.Users.SingleAsync()).Id;

        var permissions = await database.GetRequiredService<IPermissionService>()
            .GetEffectivePermissionsAsync(userId);

        Assert.Equal(PermissionCatalog.All.Count, permissions.Count);
        Assert.Contains(PermissionCatalog.SettingsRestore, permissions);
    }

    [Fact]
    public async Task User_grant_adds_and_user_revoke_removes_permissions()
    {
        using var database = new TestDatabase();
        await database.GetRequiredService<ISetupService>().CompleteAsync(TestData.CreateSetupRequest());

        string userId;
        using (var context = database.CreateContext())
        {
            userId = (await context.Users.SingleAsync()).Id;

            context.UserPermissions.AddRange(
                new UserPermission
                {
                    UserId = userId,
                    PermissionKey = PermissionCatalog.SalesVoid,
                    IsGranted = false
                },
                new UserPermission
                {
                    UserId = userId,
                    PermissionKey = PermissionCatalog.SettingsLicense,
                    IsGranted = false
                });

            await context.SaveChangesAsync();
        }

        var permissions = await database.GetRequiredService<IPermissionService>()
            .GetEffectivePermissionsAsync(userId);

        Assert.DoesNotContain(PermissionCatalog.SalesVoid, permissions);
        Assert.DoesNotContain(PermissionCatalog.SettingsLicense, permissions);
        Assert.Contains(PermissionCatalog.SalesCreate, permissions);
    }

    [Fact]
    public async Task Cashier_role_permissions_exclude_sensitive_ones()
    {
        using var database = new TestDatabase();
        await database.GetRequiredService<ISetupService>().CompleteAsync(TestData.CreateSetupRequest());

        string cashierRoleId;
        using (var context = database.CreateContext())
            cashierRoleId = (await context.Roles.SingleAsync(role => role.Name == PermissionCatalog.CashierRoleName)).Id;

        var permissions = await database.GetRequiredService<IPermissionService>()
            .GetRolePermissionsAsync(cashierRoleId);

        Assert.Contains(PermissionCatalog.SalesCreate, permissions);
        Assert.DoesNotContain(PermissionCatalog.ProductViewCostPrice, permissions);
        Assert.DoesNotContain(PermissionCatalog.SalesEditPrice, permissions);
        Assert.DoesNotContain(PermissionCatalog.ReportProfitLoss, permissions);
    }
}
