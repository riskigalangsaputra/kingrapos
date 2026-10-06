using KingraPOS.Domain.Common;
using KingraPOS.Domain.Enums;

namespace KingraPOS.Domain.Entities;

public class BackupLog : Entity
{
    public string? ScheduleId { get; set; }

    public BackupTriggerType TriggerType { get; set; }

    public string? TriggeredByUserId { get; set; }

    public JobStatus Status { get; set; } = JobStatus.RUNNING;

    public string? FileName { get; set; }

    public long? FileSizeBytes { get; set; }

    public string? ChecksumSha256 { get; set; }

    public string? Destination { get; set; }

    public string? ErrorMessage { get; set; }

    public DateTimeOffset StartedAt { get; set; }

    public DateTimeOffset? FinishedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
