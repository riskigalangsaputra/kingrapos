using KingraPOS.Application.Abstractions.Services;
using KingraPOS.Application.Dtos;
using KingraPOS.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace KingraPOS.Tests;

public class BackupServiceTests : IDisposable
{
    private readonly string _destinationFolder;

    public BackupServiceTests()
    {
        _destinationFolder = Path.Combine(Path.GetTempPath(), $"kingrapos-backup-{Guid.NewGuid():N}");
    }

    public void Dispose()
    {
        if (Directory.Exists(_destinationFolder))
            Directory.Delete(_destinationFolder, recursive: true);

        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task Backup_creates_a_sqlite_snapshot_with_checksum()
    {
        using var database = new TestDatabase();
        await database.GetRequiredService<ISetupService>().CompleteAsync(TestData.CreateSetupRequest());

        var backupService = database.GetRequiredService<IBackupService>();
        await ScheduleAsync(backupService, retention: 7);

        var log = await backupService.RunBackupAsync(BackupTriggerType.MANUAL);

        Assert.Equal(JobStatus.SUCCESS, log.Status);
        Assert.NotNull(log.FileName);
        Assert.NotNull(log.ChecksumSha256);
        Assert.Equal(64, log.ChecksumSha256!.Length);
        Assert.True(log.FileSizeBytes > 0);

        var file = Path.Combine(_destinationFolder, log.FileName!);
        Assert.True(File.Exists(file));

        var store = database.GetRequiredService<KingraPOS.Application.Abstractions.Persistence.IDatabaseFileStore>();
        Assert.True(store.IsSqliteDatabase(file));
        Assert.Equal(1, await store.GetSchemaVersionAsync(file));
    }

    [Fact]
    public async Task Backup_snapshot_contains_the_seeded_data()
    {
        using var database = new TestDatabase();
        await database.GetRequiredService<ISetupService>().CompleteAsync(TestData.CreateSetupRequest());

        var backupService = database.GetRequiredService<IBackupService>();
        await ScheduleAsync(backupService, retention: 7);

        var log = await backupService.RunBackupAsync(BackupTriggerType.MANUAL);
        var file = Path.Combine(_destinationFolder, log.FileName!);

        var options = KingraPOS.Infrastructure.Persistence.KingraPosDatabase.CreateOptions(file);
        await using var restoredContext = new KingraPOS.Infrastructure.Persistence.KingraPosDbContext(options);

        Assert.Equal(1, await restoredContext.Users.CountAsync());
        Assert.Equal("Toko Uji", (await restoredContext.BusinessProfiles.SingleAsync()).Name);
    }

    [Fact]
    public async Task Backup_records_history_and_last_backup_meta()
    {
        using var database = new TestDatabase();
        await database.GetRequiredService<ISetupService>().CompleteAsync(TestData.CreateSetupRequest());

        var backupService = database.GetRequiredService<IBackupService>();
        await ScheduleAsync(backupService, retention: 7);

        await backupService.RunBackupAsync(BackupTriggerType.MANUAL);

        var history = await backupService.GetHistoryAsync();
        Assert.Single(history);
        Assert.Equal(JobStatus.SUCCESS, history[0].Status);

        using var context = database.CreateContext();
        Assert.True(await context.BackupLogs.CountAsync(row => row.Status == JobStatus.SUCCESS) == 1);
        Assert.True(await context.AppMetaEntries.AnyAsync(row => row.Key == "last_backup_at"));

        var health = await backupService.GetHealthAsync();
        Assert.NotNull(health.LastSuccessAt);
        Assert.False(health.IsStale);
    }

    [Fact]
    public async Task Backup_updates_the_schedule_last_run_at()
    {
        using var database = new TestDatabase();
        await database.GetRequiredService<ISetupService>().CompleteAsync(TestData.CreateSetupRequest());

        var backupService = database.GetRequiredService<IBackupService>();
        await ScheduleAsync(backupService, retention: 7);

        await backupService.RunBackupAsync(BackupTriggerType.SCHEDULED);

        var schedule = await backupService.GetScheduleAsync();
        Assert.NotNull(schedule);
        Assert.NotNull(schedule!.LastRunAt);
    }

    [Fact]
    public async Task Retention_keeps_only_the_configured_number_of_files()
    {
        using var database = new TestDatabase();
        await database.GetRequiredService<ISetupService>().CompleteAsync(TestData.CreateSetupRequest());

        var backupService = database.GetRequiredService<IBackupService>();
        await ScheduleAsync(backupService, retention: 2);

        for (var index = 0; index < 4; index++)
            await backupService.RunBackupAsync(BackupTriggerType.MANUAL);

        var files = Directory.GetFiles(_destinationFolder, "kingrapos-backup-*.db");

        Assert.Equal(2, files.Length);
    }

    [Fact]
    public async Task Health_flags_a_stale_backup_when_there_is_none()
    {
        using var database = new TestDatabase();
        await database.GetRequiredService<ISetupService>().CompleteAsync(TestData.CreateSetupRequest());

        var health = await database.GetRequiredService<IBackupService>().GetHealthAsync();

        Assert.Null(health.LastSuccessAt);
        Assert.True(health.IsStale);
    }

    [Fact]
    public async Task Daily_due_time_is_within_the_last_24_hours()
    {
        using var database = new TestDatabase();
        await database.GetRequiredService<ISetupService>().CompleteAsync(TestData.CreateSetupRequest());

        var backupService = database.GetRequiredService<IBackupService>();
        await ScheduleAsync(backupService, retention: 7, frequency: BackupFrequency.DAILY, runTime: "23:00");

        var due = await backupService.GetLastDueTimeAsync();

        Assert.NotNull(due);

        var now = DateTimeOffset.UtcNow;
        Assert.True(due!.Value <= now);
        Assert.True(now - due.Value <= TimeSpan.FromHours(24));
    }

    [Fact]
    public async Task Weekly_due_time_matches_the_selected_day()
    {
        using var database = new TestDatabase();
        await database.GetRequiredService<ISetupService>().CompleteAsync(TestData.CreateSetupRequest());

        var backupService = database.GetRequiredService<IBackupService>();
        await ScheduleAsync(
            backupService,
            retention: 7,
            frequency: BackupFrequency.WEEKLY,
            runTime: "23:00",
            dayOfWeek: 1);

        var due = await backupService.GetLastDueTimeAsync();
        Assert.NotNull(due);

        var offsetMinutes = 420;
        var localDue = due!.Value.AddMinutes(offsetMinutes);

        Assert.Equal(DayOfWeek.Monday, localDue.DayOfWeek);
    }

    [Fact]
    public async Task Inactive_schedule_has_no_due_time()
    {
        using var database = new TestDatabase();
        await database.GetRequiredService<ISetupService>().CompleteAsync(TestData.CreateSetupRequest());

        var backupService = database.GetRequiredService<IBackupService>();
        await backupService.SaveScheduleAsync(new BackupScheduleRequest(
            BackupFrequency.DAILY,
            "23:00",
            null,
            7,
            _destinationFolder,
            IsActive: false));

        Assert.Null(await backupService.GetLastDueTimeAsync());
    }

    private async Task ScheduleAsync(
        IBackupService backupService,
        int retention,
        BackupFrequency frequency = BackupFrequency.DAILY,
        string runTime = "23:00",
        int? dayOfWeek = null)
    {
        await backupService.SaveScheduleAsync(new BackupScheduleRequest(
            frequency,
            runTime,
            dayOfWeek,
            retention,
            _destinationFolder,
            IsActive: true));
    }
}
