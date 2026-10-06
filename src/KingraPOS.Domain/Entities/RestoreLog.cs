using KingraPOS.Domain.Common;
using KingraPOS.Domain.Enums;

namespace KingraPOS.Domain.Entities;

public class RestoreLog : Entity
{
    public string? UserId { get; set; }

    public string SourceFile { get; set; } = string.Empty;

    public string? PreRestoreBackup { get; set; }

    public JobStatus Status { get; set; } = JobStatus.RUNNING;

    public string? ErrorMessage { get; set; }

    public DateTimeOffset StartedAt { get; set; }

    public DateTimeOffset? FinishedAt { get; set; }
}
