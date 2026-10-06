using KingraPOS.Domain.Common;
using KingraPOS.Domain.Enums;

namespace KingraPOS.Domain.Entities;

public class BackupSchedule : Entity
{
    public BackupFrequency Frequency { get; set; } = BackupFrequency.DAILY;

    public string RunTime { get; set; } = "23:00";

    public int? DayOfWeek { get; set; }

    public int RetentionCount { get; set; } = 7;

    public string DestinationPath { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateTimeOffset? LastRunAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
