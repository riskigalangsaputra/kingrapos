using KingraPOS.Application.Abstractions.Services;
using KingraPOS.Application.Dtos;
using KingraPOS.Application.Security;
using Microsoft.EntityFrameworkCore;

namespace KingraPOS.Tests;

public class AuthenticationServiceTests
{
    [Fact]
    public async Task Login_with_valid_credentials_signs_the_user_in()
    {
        using var database = new TestDatabase();
        await database.GetRequiredService<ISetupService>().CompleteAsync(TestData.CreateSetupRequest());

        var result = await database.GetRequiredService<IAuthenticationService>()
            .LoginAsync(new LoginRequest(TestData.OwnerUsername, TestData.OwnerPassword));

        Assert.True(result.Success);
        Assert.NotNull(result.User);
        Assert.Equal(PermissionCatalog.OwnerRoleName, result.User!.RoleName);

        var session = database.GetRequiredService<ICurrentUserSession>();
        Assert.True(session.IsAuthenticated);
        Assert.Contains(PermissionCatalog.SettingsBackup, session.Permissions);
    }

    [Fact]
    public async Task Login_with_wrong_password_fails_and_is_audited()
    {
        using var database = new TestDatabase();
        await database.GetRequiredService<ISetupService>().CompleteAsync(TestData.CreateSetupRequest());

        var result = await database.GetRequiredService<IAuthenticationService>()
            .LoginAsync(new LoginRequest(TestData.OwnerUsername, "password-salah"));

        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);

        Assert.False(database.GetRequiredService<ICurrentUserSession>().IsAuthenticated);

        using var context = database.CreateContext();
        var failedLogs = await context.ActivityLogs
            .Where(log => log.ActionType == "LOGIN_FAILED")
            .CountAsync();

        Assert.Equal(1, failedLogs);
    }

    [Fact]
    public async Task Login_with_unknown_username_fails()
    {
        using var database = new TestDatabase();
        await database.GetRequiredService<ISetupService>().CompleteAsync(TestData.CreateSetupRequest());

        var result = await database.GetRequiredService<IAuthenticationService>()
            .LoginAsync(new LoginRequest("tidak-ada", TestData.OwnerPassword));

        Assert.False(result.Success);
    }

    [Fact]
    public async Task Login_updates_last_login_at_and_writes_success_log()
    {
        using var database = new TestDatabase();
        await database.GetRequiredService<ISetupService>().CompleteAsync(TestData.CreateSetupRequest());

        await database.GetRequiredService<IAuthenticationService>()
            .LoginAsync(new LoginRequest(TestData.OwnerUsername, TestData.OwnerPassword));

        using var context = database.CreateContext();

        Assert.NotNull((await context.Users.SingleAsync()).LastLoginAt);
        Assert.Equal(1, await context.ActivityLogs.CountAsync(log => log.ActionType == "LOGIN_SUCCESS"));
    }

    [Fact]
    public async Task ChangePassword_replaces_the_stored_hash()
    {
        using var database = new TestDatabase();
        await database.GetRequiredService<ISetupService>().CompleteAsync(TestData.CreateSetupRequest());

        string userId;
        using (var context = database.CreateContext())
            userId = (await context.Users.SingleAsync()).Id;

        var authenticationService = database.GetRequiredService<IAuthenticationService>();

        Assert.False(await authenticationService.ChangePasswordAsync(userId, "salah", "password-baru"));
        Assert.True(await authenticationService.ChangePasswordAsync(userId, TestData.OwnerPassword, "password-baru"));

        var login = await authenticationService.LoginAsync(new LoginRequest(TestData.OwnerUsername, "password-baru"));
        Assert.True(login.Success);
    }
}
