using KingraPOS.Application.Abstractions.Services;
using KingraPOS.Application.Dtos;
using KingraPOS.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace KingraPOS.Tests;

public class RestoreServiceTests : IDisposable
{
    private readonly string _backupFolder;

    public RestoreServiceTests()
    {
        _backupFolder = Path.Combine(Path.GetTempPath(), $"kingrapos-restore-{Guid.NewGuid():N}");
    }

    public void Dispose()
    {
        if (Directory.Exists(_backupFolder))
            Directory.Delete(_backupFolder, recursive: true);

        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task Inspect_rejects_a_missing_file()
    {
        using var database = new TestDatabase();

        var inspection = await database.GetRequiredService<IRestoreService>()
            .InspectAsync(Path.Combine(_backupFolder, "tidak-ada.db"));

        Assert.False(inspection.IsValid);
    }

    [Fact]
    public async Task Inspect_rejects_a_file_that_is_not_a_database()
    {
        using var database = new TestDatabase();
        Directory.CreateDirectory(_backupFolder);

        var file = Path.Combine(_backupFolder, "bukan-database.db");
        await File.WriteAllTextAsync(file, "ini hanya teks biasa");

        var inspection = await database.GetRequiredService<IRestoreService>().InspectAsync(file);

        Assert.False(inspection.IsValid);
        Assert.Contains("SQLite", inspection.Message);
    }

    [Fact]
    public async Task Inspect_accepts_a_valid_backup_and_reports_the_schema_version()
    {
        using var database = new TestDatabase();
        await database.GetRequiredService<ISetupService>().CompleteAsync(TestData.CreateSetupRequest());

        var backupFile = await CreateBackupAsync(database);
        var inspection = await database.GetRequiredService<IRestoreService>().InspectAsync(backupFile);

        Assert.True(inspection.IsValid);
        Assert.Equal(1, inspection.SchemaVersion);
    }

    [Fact]
    public async Task Restore_replaces_data_and_creates_a_pre_restore_backup()
    {
        using var database = new TestDatabase();
        await database.GetRequiredService<ISetupService>().CompleteAsync(TestData.CreateSetupRequest());

        var backupFile = await CreateBackupAsync(database);

        // ubah data setelah backup dibuat
        await database.GetRequiredService<IAuthenticationService>()
            .LoginAsync(new LoginRequest(TestData.OwnerUsername, TestData.OwnerPassword));

        var userService = database.GetRequiredService<IUserManagementService>();
        var roles = await userService.GetRolesAsync();
        await userService.CreateUserAsync(new CreateUserRequest(
            "Kasir Tambahan",
            "kasir-tambahan",
            "rahasia123",
            roles.Single(role => role.Name == "Kasir").Id,
            null));

        using (var context = database.CreateContext())
            Assert.Equal(2, await context.Users.CountAsync());

        var result = await database.GetRequiredService<IRestoreService>().RestoreAsync(backupFile);

        Assert.True(result.Success);
        Assert.NotNull(result.PreRestoreBackupPath);
        Assert.True(File.Exists(result.PreRestoreBackupPath!));

        using (var context = database.CreateContext())
        {
            Assert.Equal(1, await context.Users.CountAsync());

            var restoreLog = await context.RestoreLogs.SingleAsync();
            Assert.Equal(JobStatus.SUCCESS, restoreLog.Status);
            Assert.NotNull(restoreLog.PreRestoreBackup);
        }
    }

    [Fact]
    public async Task Restore_fails_for_an_invalid_file_and_records_nothing()
    {
        using var database = new TestDatabase();
        await database.GetRequiredService<ISetupService>().CompleteAsync(TestData.CreateSetupRequest());

        var result = await database.GetRequiredService<IRestoreService>()
            .RestoreAsync(Path.Combine(_backupFolder, "hilang.db"));

        Assert.False(result.Success);

        using var context = database.CreateContext();
        Assert.Equal(0, await context.RestoreLogs.CountAsync());
    }

    private async Task<string> CreateBackupAsync(TestDatabase database)
    {
        var backupService = database.GetRequiredService<IBackupService>();

        await backupService.SaveScheduleAsync(new BackupScheduleRequest(
            BackupFrequency.DAILY,
            "23:00",
            null,
            7,
            _backupFolder,
            IsActive: true));

        var log = await backupService.RunBackupAsync(BackupTriggerType.MANUAL);

        Assert.Equal(JobStatus.SUCCESS, log.Status);

        return Path.Combine(_backupFolder, log.FileName!);
    }
}
