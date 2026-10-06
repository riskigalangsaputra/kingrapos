using KingraPOS.Application.Abstractions.Persistence;
using KingraPOS.Application.Abstractions.Security;
using KingraPOS.Application.Abstractions.Services;
using KingraPOS.Application.Dtos;
using KingraPOS.Domain.Entities;
using KingraPOS.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace KingraPOS.Application.Services;

public sealed class BackupService : IBackupService
{
    private const string BackupFilePrefix = "kingrapos-backup-";
    private const string BackupFilePattern = "kingrapos-backup-*.db";
    private const string LastBackupMetaKey = "last_backup_at";
    private const string MainRowId = "main";

    private readonly IKingraPosDbContextFactory _contextFactory;
    private readonly IDatabaseFileStore _fileStore;
    private readonly IActivityLogService _activityLogService;
    private readonly IClock _clock;

    public BackupService(
        IKingraPosDbContextFactory contextFactory,
        IDatabaseFileStore fileStore,
        IActivityLogService activityLogService,
        IClock clock)
    {
        _contextFactory = contextFactory;
        _fileStore = fileStore;
        _activityLogService = activityLogService;
        _clock = clock;
    }

    public Task<string> GetDefaultDestinationAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(_fileStore.GetDefaultBackupFolder());

    public async Task<BackupScheduleDto?> GetScheduleAsync(CancellationToken cancellationToken = default)
    {
        using var context = _contextFactory.Create();

        var schedule = await context.BackupSchedules
            .OrderByDescending(row => row.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        return schedule is null
            ? null
            : new BackupScheduleDto(
                schedule.Id,
                schedule.Frequency,
                schedule.RunTime,
                schedule.DayOfWeek,
                schedule.RetentionCount,
                schedule.DestinationPath,
                schedule.IsActive,
                schedule.LastRunAt);
    }

    public async Task SaveScheduleAsync(BackupScheduleRequest request, CancellationToken cancellationToken = default)
    {
        using var context = _contextFactory.Create();

        var now = _clock.UtcNow;

        var schedule = await context.BackupSchedules
            .OrderByDescending(row => row.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (schedule is null)
        {
            schedule = new BackupSchedule { CreatedAt = now };
            context.BackupSchedules.Add(schedule);
        }

        schedule.Frequency = request.Frequency;
        schedule.RunTime = request.RunTime.Trim();
        schedule.DayOfWeek = request.Frequency == BackupFrequency.WEEKLY ? request.DayOfWeek : null;
        schedule.RetentionCount = request.RetentionCount;
        schedule.DestinationPath = request.DestinationPath.Trim();
        schedule.IsActive = request.IsActive;
        schedule.UpdatedAt = now;

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<BackupLogDto> RunBackupAsync(
        BackupTriggerType triggerType,
        string? userId = null,
        CancellationToken cancellationToken = default)
    {
        var schedule = await GetScheduleAsync(cancellationToken);
        var destinationFolder = schedule?.DestinationPath;

        if (string.IsNullOrWhiteSpace(destinationFolder))
            destinationFolder = _fileStore.GetDefaultBackupFolder();

        Directory.CreateDirectory(destinationFolder);

        var startedAt = _clock.UtcNow;
        var fileName = $"{BackupFilePrefix}{startedAt:yyyyMMdd-HHmmssfff}.db";
        var fullPath = Path.Combine(destinationFolder, fileName);

        using var context = _contextFactory.Create();

        var log = new BackupLog
        {
            TriggerType = triggerType,
            TriggeredByUserId = userId,
            Status = JobStatus.RUNNING,
            FileName = fileName,
            Destination = destinationFolder,
            StartedAt = startedAt,
            CreatedAt = startedAt
        };

        context.BackupLogs.Add(log);
        await context.SaveChangesAsync(cancellationToken);

        try
        {
            if (File.Exists(fullPath))
                File.Delete(fullPath);

            await _fileStore.CreateSnapshotAsync(fullPath, cancellationToken);

            log.FileSizeBytes = new FileInfo(fullPath).Length;
            log.ChecksumSha256 = await _fileStore.ComputeSha256Async(fullPath, cancellationToken);
            log.Status = JobStatus.SUCCESS;

            await UpdateLastBackupMetaAsync(context, startedAt, cancellationToken);
            ApplyRetention(destinationFolder, schedule?.RetentionCount ?? 7);

            if (schedule is not null)
            {
                var tracked = await context.BackupSchedules
                    .FirstOrDefaultAsync(row => row.Id == schedule.Id, cancellationToken);

                if (tracked is not null)
                {
                    tracked.LastRunAt = startedAt;
                    tracked.UpdatedAt = startedAt;
                }
            }
        }
        catch (Exception exception)
        {
            log.Status = JobStatus.FAILED;
            log.ErrorMessage = exception.Message;
        }
        finally
        {
            log.FinishedAt = _clock.UtcNow;
            await context.SaveChangesAsync(cancellationToken);
        }

        if (log.Status == JobStatus.SUCCESS)
        {
            await _activityLogService.LogAsync(
                "BACKUP_COMPLETED",
                $"Backup {(triggerType == BackupTriggerType.SCHEDULED ? "terjadwal" : "manual")} berhasil: {fileName}.",
                userId: userId,
                entityType: "backup_logs",
                entityId: log.Id,
                cancellationToken: cancellationToken);
        }

        return ToDto(log);
    }

    public async Task<IReadOnlyList<BackupLogDto>> GetHistoryAsync(
        int take = 50,
        CancellationToken cancellationToken = default)
    {
        using var context = _contextFactory.Create();

        var logs = await context.BackupLogs
            .OrderByDescending(row => row.StartedAt)
            .Take(take)
            .ToListAsync(cancellationToken);

        return logs.Select(ToDto).ToList();
    }

    public async Task<BackupHealthDto> GetHealthAsync(CancellationToken cancellationToken = default)
    {
        using var context = _contextFactory.Create();

        var lastSuccess = await context.BackupLogs
            .Where(row => row.Status == JobStatus.SUCCESS)
            .OrderByDescending(row => row.StartedAt)
            .Select(row => (DateTimeOffset?)row.StartedAt)
            .FirstOrDefaultAsync(cancellationToken);

        var lastFailure = await context.BackupLogs
            .Where(row => row.Status == JobStatus.FAILED)
            .OrderByDescending(row => row.StartedAt)
            .Select(row => new { row.StartedAt, row.ErrorMessage })
            .FirstOrDefaultAsync(cancellationToken);

        var schedule = await GetScheduleAsync(cancellationToken);
        var threshold = GetStaleThreshold(schedule);
        var now = _clock.UtcNow;

        var isStale = schedule?.IsActive != false
            && (lastSuccess is null || now - lastSuccess.Value > threshold);

        var lastError = lastFailure is not null && (lastSuccess is null || lastFailure.StartedAt > lastSuccess.Value)
            ? lastFailure.ErrorMessage
            : null;

        return new BackupHealthDto(lastSuccess, isStale, threshold, lastError);
    }

    public async Task<DateTimeOffset?> GetLastDueTimeAsync(CancellationToken cancellationToken = default)
    {
        var schedule = await GetScheduleAsync(cancellationToken);
        if (schedule is null || !schedule.IsActive)
            return null;

        if (!TryParseRunTime(schedule.RunTime, out var hour, out var minute))
            return null;

        var offsetMinutes = await GetUtcOffsetMinutesAsync(cancellationToken);
        var nowLocal = _clock.UtcNow.AddMinutes(offsetMinutes).DateTime;

        var localDue = schedule.Frequency switch
        {
            BackupFrequency.HOURLY => ComputeHourlyDue(nowLocal, minute),
            BackupFrequency.DAILY => ComputeDailyDue(nowLocal, hour, minute),
            BackupFrequency.WEEKLY => ComputeWeeklyDue(nowLocal, hour, minute, schedule.DayOfWeek ?? 0),
            _ => (DateTime?)null
        };

        return localDue is null ? null : ToUtc(localDue.Value, offsetMinutes);
    }

    internal static TimeSpan GetStaleThreshold(BackupScheduleDto? schedule)
    {
        var interval = schedule?.Frequency switch
        {
            BackupFrequency.HOURLY => TimeSpan.FromHours(1),
            BackupFrequency.WEEKLY => TimeSpan.FromDays(7),
            _ => TimeSpan.FromDays(1)
        };

        var threshold = interval * 2;

        return threshold < TimeSpan.FromHours(24) ? TimeSpan.FromHours(24) : threshold;
    }

    private async Task UpdateLastBackupMetaAsync(
        IKingraPosDbContext context,
        DateTimeOffset timestamp,
        CancellationToken cancellationToken)
    {
        var meta = await context.AppMetaEntries
            .FirstOrDefaultAsync(row => row.Key == LastBackupMetaKey, cancellationToken);

        if (meta is null)
        {
            meta = new AppMeta { Key = LastBackupMetaKey };
            context.AppMetaEntries.Add(meta);
        }

        meta.Value = timestamp.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
        meta.UpdatedAt = timestamp;

        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task<int> GetUtcOffsetMinutesAsync(CancellationToken cancellationToken)
    {
        using var context = _contextFactory.Create();

        var offset = await context.Settings
            .Where(row => row.Id == MainRowId)
            .Select(row => (int?)row.UtcOffsetMinutes)
            .FirstOrDefaultAsync(cancellationToken);

        return offset ?? 420;
    }

    private static void ApplyRetention(string folder, int retentionCount)
    {
        var keep = Math.Max(retentionCount, 1);

        var files = new DirectoryInfo(folder)
            .EnumerateFiles(BackupFilePattern, SearchOption.TopDirectoryOnly)
            .OrderByDescending(file => file.Name, StringComparer.Ordinal)
            .ToList();

        foreach (var file in files.Skip(keep))
        {
            try
            {
                file.Delete();
            }
            catch (IOException)
            {
                // file sedang dipakai proses lain — lewati
            }
        }
    }

    private static bool TryParseRunTime(string runTime, out int hour, out int minute)
    {
        hour = 0;
        minute = 0;

        var parts = runTime.Split(':');
        return parts.Length == 2
            && int.TryParse(parts[0], out hour)
            && int.TryParse(parts[1], out minute)
            && hour is >= 0 and <= 23
            && minute is >= 0 and <= 59;
    }

    private static DateTime ComputeHourlyDue(DateTime nowLocal, int minute)
    {
        var candidate = new DateTime(nowLocal.Year, nowLocal.Month, nowLocal.Day, nowLocal.Hour, minute, 0);

        return candidate > nowLocal ? candidate.AddHours(-1) : candidate;
    }

    private static DateTime ComputeDailyDue(DateTime nowLocal, int hour, int minute)
    {
        var candidate = new DateTime(nowLocal.Year, nowLocal.Month, nowLocal.Day, hour, minute, 0);

        return candidate > nowLocal ? candidate.AddDays(-1) : candidate;
    }

    private static DateTime ComputeWeeklyDue(DateTime nowLocal, int hour, int minute, int dayOfWeek)
    {
        var candidate = new DateTime(nowLocal.Year, nowLocal.Month, nowLocal.Day, hour, minute, 0);

        var delta = ((int)candidate.DayOfWeek - dayOfWeek + 7) % 7;
        candidate = candidate.AddDays(-delta);

        return candidate > nowLocal ? candidate.AddDays(-7) : candidate;
    }

    private static DateTimeOffset ToUtc(DateTime localDue, int offsetMinutes) =>
        new DateTimeOffset(DateTime.SpecifyKind(localDue, DateTimeKind.Unspecified), TimeSpan.Zero)
            .AddMinutes(-offsetMinutes);

    private static BackupLogDto ToDto(BackupLog log) => new(
        log.Id,
        log.TriggerType,
        log.Status,
        log.FileName,
        log.FileSizeBytes,
        log.ChecksumSha256,
        log.Destination,
        log.ErrorMessage,
        log.StartedAt,
        log.FinishedAt);
}
