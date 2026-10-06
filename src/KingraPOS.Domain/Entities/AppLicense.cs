using KingraPOS.Domain.Common;
using KingraPOS.Domain.Enums;

namespace KingraPOS.Domain.Entities;

public class AppLicense : Entity
{
    public string? LicenseKey { get; set; }

    public string? LicensedTo { get; set; }

    public string Edition { get; set; } = "OFFLINE";

    public LicenseStatus Status { get; set; } = LicenseStatus.TRIAL;

    public string? DeviceFingerprint { get; set; }

    public DateTimeOffset? ActivatedAt { get; set; }

    public DateTimeOffset? ExpiresAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
