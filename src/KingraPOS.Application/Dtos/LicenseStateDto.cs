using KingraPOS.Domain.Enums;

namespace KingraPOS.Application.Dtos;

public enum LicenseAccessLevel
{
    Full = 0,
    Grace = 1,
    Restricted = 2
}

public record LicenseStateDto(
    LicenseStatus Status,
    LicenseAccessLevel AccessLevel,
    string? LicensedTo,
    string? MaskedKey,
    DateTimeOffset? ActivatedAt,
    DateTimeOffset? ExpiresAt,
    int? DaysRemaining,
    DateTimeOffset? GraceEndsAt,
    string? DeviceFingerprint)
{
    public bool IsWriteAllowed => AccessLevel != LicenseAccessLevel.Restricted;

    public bool HasLicenseKey => !string.IsNullOrWhiteSpace(MaskedKey);
}

public record LicenseActivationRequest(string LicenseKey, string? LicensedTo, DateOnly? ExpiresAt);
