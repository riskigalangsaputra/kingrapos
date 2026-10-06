using KingraPOS.Domain.Enums;

namespace KingraPOS.Application.Dtos;

public record BackupScheduleDto(
    string? Id,
    BackupFrequency Frequency,
    string RunTime,
    int? DayOfWeek,
    int RetentionCount,
    string DestinationPath,
    bool IsActive,
    DateTimeOffset? LastRunAt);

public record BackupScheduleRequest(
    BackupFrequency Frequency,
    string RunTime,
    int? DayOfWeek,
    int RetentionCount,
    string DestinationPath,
    bool IsActive);

public record BackupLogDto(
    string Id,
    BackupTriggerType TriggerType,
    JobStatus Status,
    string? FileName,
    long? FileSizeBytes,
    string? ChecksumSha256,
    string? Destination,
    string? ErrorMessage,
    DateTimeOffset StartedAt,
    DateTimeOffset? FinishedAt);

public record BackupHealthDto(
    DateTimeOffset? LastSuccessAt,
    bool IsStale,
    TimeSpan? StaleThreshold,
    string? LastError);

public record RestoreInspectionDto(bool IsValid, int SchemaVersion, string Message);

public record RestoreResultDto(bool Success, string? PreRestoreBackupPath, string Message);
