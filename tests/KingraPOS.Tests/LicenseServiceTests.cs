using KingraPOS.Application.Abstractions.Services;
using KingraPOS.Application.Dtos;
using KingraPOS.Application.Security;
using KingraPOS.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace KingraPOS.Tests;

public class LicenseServiceTests
{
    [Fact]
    public async Task Setup_starts_a_thirty_day_trial()
    {
        using var database = new TestDatabase();
        await database.GetRequiredService<ISetupService>().CompleteAsync(TestData.CreateSetupRequest());

        var state = await database.GetRequiredService<ILicenseService>().GetStateAsync();

        Assert.Equal(LicenseStatus.TRIAL, state.Status);
        Assert.Equal(LicenseAccessLevel.Full, state.AccessLevel);
        Assert.True(state.IsWriteAllowed);
        Assert.NotNull(state.ExpiresAt);
        Assert.InRange(state.DaysRemaining ?? 0, 29, 30);
        Assert.False(string.IsNullOrWhiteSpace(state.DeviceFingerprint));
    }

    [Fact]
    public async Task Expired_within_grace_still_allows_writes()
    {
        using var database = new TestDatabase();
        await database.GetRequiredService<ISetupService>().CompleteAsync(TestData.CreateSetupRequest());
        await SetExpiryAsync(database, DateTimeOffset.UtcNow.AddDays(-3), LicenseStatus.ACTIVE);

        var state = await database.GetRequiredService<ILicenseService>().GetStateAsync();

        Assert.Equal(LicenseAccessLevel.Grace, state.AccessLevel);
        Assert.True(state.IsWriteAllowed);
        Assert.True(state.DaysRemaining < 0);
    }

    [Fact]
    public async Task Past_grace_restricts_writes_and_persists_expired_status()
    {
        using var database = new TestDatabase();
        await database.GetRequiredService<ISetupService>().CompleteAsync(TestData.CreateSetupRequest());
        await SetExpiryAsync(database, DateTimeOffset.UtcNow.AddDays(-30), LicenseStatus.ACTIVE);

        var licenseService = database.GetRequiredService<ILicenseService>();
        var state = await licenseService.GetStateAsync();

        Assert.Equal(LicenseAccessLevel.Restricted, state.AccessLevel);
        Assert.Equal(LicenseStatus.EXPIRED, state.Status);
        Assert.False(state.IsWriteAllowed);

        using var context = database.CreateContext();
        Assert.Equal(LicenseStatus.EXPIRED, (await context.AppLicenses.SingleAsync()).Status);
    }

    [Fact]
    public async Task Revoked_is_restricted_immediately()
    {
        using var database = new TestDatabase();
        await database.GetRequiredService<ISetupService>().CompleteAsync(TestData.CreateSetupRequest());
        await SetExpiryAsync(database, DateTimeOffset.UtcNow.AddDays(365), LicenseStatus.REVOKED);

        var state = await database.GetRequiredService<ILicenseService>().GetStateAsync();

        Assert.Equal(LicenseStatus.REVOKED, state.Status);
        Assert.Equal(LicenseAccessLevel.Restricted, state.AccessLevel);
    }

    [Fact]
    public async Task Activating_a_perpetual_license_clears_the_restriction()
    {
        using var database = new TestDatabase();
        await database.GetRequiredService<ISetupService>().CompleteAsync(TestData.CreateSetupRequest());
        await SetExpiryAsync(database, DateTimeOffset.UtcNow.AddDays(-60), LicenseStatus.ACTIVE);

        var licenseService = database.GetRequiredService<ILicenseService>();
        Assert.False(await licenseService.IsWriteAllowedAsync());

        var state = await licenseService.ActivateAsync(
            new LicenseActivationRequest("KINGRA-1234-ABCD", "Toko Uji", null));

        Assert.Equal(LicenseStatus.ACTIVE, state.Status);
        Assert.Equal(LicenseAccessLevel.Full, state.AccessLevel);
        Assert.Null(state.ExpiresAt);
        Assert.Null(state.DaysRemaining);
        Assert.True(state.IsWriteAllowed);
        Assert.Equal("Toko Uji", state.LicensedTo);
        Assert.Equal("\u2022\u2022\u2022\u2022\u2022\u2022\u2022\u2022ABCD", state.MaskedKey);
    }

    [Fact]
    public async Task Activating_a_dated_license_uses_the_given_expiry()
    {
        using var database = new TestDatabase();
        await database.GetRequiredService<ISetupService>().CompleteAsync(TestData.CreateSetupRequest());

        var expiry = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(1));
        var state = await database.GetRequiredService<ILicenseService>()
            .ActivateAsync(new LicenseActivationRequest("KEY-0001", null, expiry));

        Assert.Equal(LicenseAccessLevel.Full, state.AccessLevel);
        Assert.NotNull(state.ExpiresAt);
        Assert.Equal(expiry.Year, state.ExpiresAt!.Value.Year);
        Assert.Equal(expiry.Day, state.ExpiresAt!.Value.Day);
    }

    [Fact]
    public async Task Restrictive_license_blocks_creating_a_user()
    {
        using var database = new TestDatabase();
        await database.GetRequiredService<ISetupService>().CompleteAsync(TestData.CreateSetupRequest());
        await database.GetRequiredService<IAuthenticationService>()
            .LoginAsync(new LoginRequest(TestData.OwnerUsername, TestData.OwnerPassword));
        await SetExpiryAsync(database, DateTimeOffset.UtcNow.AddDays(-60), LicenseStatus.ACTIVE);

        var userService = database.GetRequiredService<IUserManagementService>();
        var roles = await userService.GetRolesAsync();
        var cashierRole = roles.Single(role => role.Name == PermissionCatalog.CashierRoleName);

        await Assert.ThrowsAsync<LicenseRestrictedException>(() => userService.CreateUserAsync(
            new CreateUserRequest("Kasir Satu", "kasir1", "rahasia123", cashierRole.Id, null)));
    }

    [Fact]
    public async Task Activation_is_recorded_in_the_activity_log()
    {
        using var database = new TestDatabase();
        await database.GetRequiredService<ISetupService>().CompleteAsync(TestData.CreateSetupRequest());

        await database.GetRequiredService<ILicenseService>()
            .ActivateAsync(new LicenseActivationRequest("KEY-0001", "Toko Uji", null));

        using var context = database.CreateContext();

        Assert.Equal(1, await context.ActivityLogs.CountAsync(log => log.ActionType == "LICENSE_ACTIVATED"));
    }

    private static async Task SetExpiryAsync(
        TestDatabase database,
        DateTimeOffset expiresAt,
        LicenseStatus status)
    {
        using var context = database.CreateContext();

        var license = await context.AppLicenses.SingleAsync();
        license.ExpiresAt = expiresAt;
        license.Status = status;

        await context.SaveChangesAsync();
    }
}
