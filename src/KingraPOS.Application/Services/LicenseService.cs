using KingraPOS.Application.Abstractions.Persistence;
using KingraPOS.Application.Abstractions.Security;
using KingraPOS.Application.Abstractions.Services;
using KingraPOS.Application.Dtos;
using KingraPOS.Application.Security;
using KingraPOS.Domain.Entities;
using KingraPOS.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace KingraPOS.Application.Services;

public sealed class LicenseService : ILicenseService
{
    private const string MainRowId = "main";

    private readonly IKingraPosDbContextFactory _contextFactory;
    private readonly IDeviceFingerprintProvider _deviceFingerprintProvider;
    private readonly IActivityLogService _activityLogService;
    private readonly IClock _clock;

    public LicenseService(
        IKingraPosDbContextFactory contextFactory,
        IDeviceFingerprintProvider deviceFingerprintProvider,
        IActivityLogService activityLogService,
        IClock clock)
    {
        _contextFactory = contextFactory;
        _deviceFingerprintProvider = deviceFingerprintProvider;
        _activityLogService = activityLogService;
        _clock = clock;
    }

    public async Task<LicenseStateDto> GetStateAsync(CancellationToken cancellationToken = default)
    {
        using var context = _contextFactory.Create();

        var license = await context.AppLicenses
            .FirstOrDefaultAsync(row => row.Id == MainRowId, cancellationToken);

        var now = _clock.UtcNow;

        if (license is null)
        {
            return new LicenseStateDto(
                LicenseStatus.TRIAL,
                LicenseAccessLevel.Restricted,
                null,
                null,
                null,
                null,
                null,
                null,
                null);
        }

        var state = Evaluate(license, now);

        if (license.Status != state.Status)
        {
            license.Status = state.Status;
            license.UpdatedAt = now;
            await context.SaveChangesAsync(cancellationToken);
        }

        return state;
    }

    public async Task<LicenseStateDto> ActivateAsync(
        LicenseActivationRequest request,
        CancellationToken cancellationToken = default)
    {
        using var context = _contextFactory.Create();

        var now = _clock.UtcNow;

        var license = await context.AppLicenses
            .FirstOrDefaultAsync(row => row.Id == MainRowId, cancellationToken);

        if (license is null)
        {
            license = new AppLicense { Id = MainRowId, CreatedAt = now };
            context.AppLicenses.Add(license);
        }

        license.LicenseKey = request.LicenseKey.Trim();
        license.LicensedTo = string.IsNullOrWhiteSpace(request.LicensedTo) ? null : request.LicensedTo.Trim();
        license.Edition = "OFFLINE";
        license.Status = LicenseStatus.ACTIVE;
        license.ActivatedAt = now;
        license.ExpiresAt = ToEndOfDayUtc(request.ExpiresAt);
        license.DeviceFingerprint = _deviceFingerprintProvider.GetFingerprint();
        license.UpdatedAt = now;

        await context.SaveChangesAsync(cancellationToken);

        await _activityLogService.LogAsync(
            "LICENSE_ACTIVATED",
            license.ExpiresAt is null
                ? "Lisensi diaktifkan tanpa batas waktu."
                : $"Lisensi diaktifkan sampai {license.ExpiresAt:yyyy-MM-dd}.",
            entityType: "app_license",
            entityId: MainRowId,
            cancellationToken: cancellationToken);

        return Evaluate(license, now);
    }

    public async Task<bool> IsWriteAllowedAsync(CancellationToken cancellationToken = default) =>
        (await GetStateAsync(cancellationToken)).IsWriteAllowed;

    public async Task EnsureWriteAllowedAsync(CancellationToken cancellationToken = default)
    {
        if (!await IsWriteAllowedAsync(cancellationToken))
            throw LicenseRestrictedException.ForWrite();
    }

    private static DateTimeOffset? ToEndOfDayUtc(DateOnly? date) =>
        date is null
            ? null
            : new DateTimeOffset(date.Value.Year, date.Value.Month, date.Value.Day, 23, 59, 59, TimeSpan.Zero);

    private static LicenseStateDto Evaluate(AppLicense license, DateTimeOffset now)
    {
        if (license.Status == LicenseStatus.REVOKED)
            return Create(license, LicenseStatus.REVOKED, LicenseAccessLevel.Restricted, now);

        if (license.ExpiresAt is null)
            return Create(license, NormalizeActive(license.Status), LicenseAccessLevel.Full, now);

        if (now <= license.ExpiresAt.Value)
            return Create(license, NormalizeActive(license.Status), LicenseAccessLevel.Full, now);

        if (now <= license.ExpiresAt.Value.Add(LicensePolicy.GracePeriod))
            return Create(license, NormalizeActive(license.Status), LicenseAccessLevel.Grace, now);

        return Create(license, LicenseStatus.EXPIRED, LicenseAccessLevel.Restricted, now);
    }

    private static LicenseStatus NormalizeActive(LicenseStatus status) =>
        status == LicenseStatus.EXPIRED ? LicenseStatus.ACTIVE : status;

    private static LicenseStateDto Create(
        AppLicense license,
        LicenseStatus status,
        LicenseAccessLevel accessLevel,
        DateTimeOffset now)
    {
        int? daysRemaining = null;

        if (license.ExpiresAt is not null)
            daysRemaining = (int)Math.Ceiling((license.ExpiresAt.Value - now).TotalDays);

        return new LicenseStateDto(
            status,
            accessLevel,
            license.LicensedTo,
            MaskKey(license.LicenseKey),
            license.ActivatedAt,
            license.ExpiresAt,
            daysRemaining,
            license.ExpiresAt?.Add(LicensePolicy.GracePeriod),
            license.DeviceFingerprint);
    }

    private static string? MaskKey(string? licenseKey)
    {
        if (string.IsNullOrWhiteSpace(licenseKey))
            return null;

        var trimmed = licenseKey.Trim();

        return trimmed.Length <= 4
            ? new string('\u2022', 8)
            : $"{new string('\u2022', 8)}{trimmed[^4..]}";
    }
}
