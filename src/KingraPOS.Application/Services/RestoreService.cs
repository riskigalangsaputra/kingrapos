using KingraPOS.Application.Abstractions.Persistence;
using KingraPOS.Application.Abstractions.Security;
using KingraPOS.Application.Abstractions.Services;
using KingraPOS.Application.Dtos;
using KingraPOS.Application.Persistence;
using KingraPOS.Domain.Entities;
using KingraPOS.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KingraPOS.Application.Services;

public sealed class RestoreService : IRestoreService
{
    private readonly IKingraPosDbContextFactory _contextFactory;
    private readonly IDatabaseFileStore _fileStore;
    private readonly IActivityLogService _activityLogService;
    private readonly ILogger<RestoreService> _logger;
    private readonly IClock _clock;

    public RestoreService(
        IKingraPosDbContextFactory contextFactory,
        IDatabaseFileStore fileStore,
        IActivityLogService activityLogService,
        ILogger<RestoreService> logger,
        IClock clock)
    {
        _contextFactory = contextFactory;
        _fileStore = fileStore;
        _activityLogService = activityLogService;
        _logger = logger;
        _clock = clock;
    }

    public async Task<RestoreInspectionDto> InspectAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            return new RestoreInspectionDto(false, 0, "File backup tidak ditemukan.");

        if (!_fileStore.IsSqliteDatabase(filePath))
            return new RestoreInspectionDto(false, 0, "File tersebut bukan database SQLite yang valid.");

        var version = await _fileStore.GetSchemaVersionAsync(filePath, cancellationToken);

        var message = version == DatabaseSchema.CurrentVersion
            ? "File valid dan siap dipulihkan."
            : $"Versi skema file ({version}) berbeda dari versi aplikasi ({DatabaseSchema.CurrentVersion}). Restore tetap dimungkinkan, tetapi aplikasi mungkin perlu penyesuaian.";

        return new RestoreInspectionDto(true, version, message);
    }

    public async Task<RestoreResultDto> RestoreAsync(
        string filePath,
        string? userId = null,
        CancellationToken cancellationToken = default)
    {
        var inspection = await InspectAsync(filePath, cancellationToken);
        if (!inspection.IsValid)
            return new RestoreResultDto(false, null, inspection.Message);

        var startedAt = _clock.UtcNow;
        string? preRestoreBackup = null;
        string? error = null;

        try
        {
            var folder = _fileStore.GetDefaultBackupFolder();
            Directory.CreateDirectory(folder);

            preRestoreBackup = Path.Combine(folder, $"kingrapos-prerestore-{startedAt:yyyyMMdd-HHmmssfff}.db");

            if (File.Exists(preRestoreBackup))
                File.Delete(preRestoreBackup);

            await _fileStore.CreateSnapshotAsync(preRestoreBackup, cancellationToken);
            await _fileStore.ReplaceDatabaseAsync(filePath, cancellationToken);
        }
        catch (Exception exception)
        {
            error = exception.Message;
        }

        var success = error is null;

        // Log ditulis SETELAH penggantian file, ke database yang berlaku sekarang:
        // saat sukses itu database hasil restore, saat gagal itu database asli yang belum tergantikan.
        try
        {
            using var context = _contextFactory.Create();

            context.RestoreLogs.Add(new RestoreLog
            {
                UserId = await ResolveExistingUserIdAsync(context, userId, cancellationToken),
                SourceFile = filePath,
                PreRestoreBackup = preRestoreBackup,
                Status = success ? JobStatus.SUCCESS : JobStatus.FAILED,
                ErrorMessage = error,
                StartedAt = startedAt,
                FinishedAt = _clock.UtcNow
            });

            await context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception logException)
        {
            _logger.LogWarning(logException, "Gagal menulis riwayat restore ke database.");
        }

        try
        {
            await _activityLogService.LogAsync(
                success ? "RESTORE_COMPLETED" : "RESTORE_FAILED",
                success
                    ? $"Database dipulihkan dari '{Path.GetFileName(filePath)}'. Backup pra-restore: '{Path.GetFileName(preRestoreBackup)}'."
                    : $"Restore gagal: {error}",
                userId: userId,
                entityType: "restore_logs",
                cancellationToken: cancellationToken);
        }
        catch (Exception logException)
        {
            _logger.LogWarning(logException, "Gagal menulis audit restore.");
        }

        return success
            ? new RestoreResultDto(
                true,
                preRestoreBackup,
                "Restore berhasil. Aplikasi akan ditutup; jalankan lagi untuk memakai data hasil restore.")
            : new RestoreResultDto(false, preRestoreBackup, $"Restore gagal: {error}");
    }

    private static async Task<string?> ResolveExistingUserIdAsync(
        IKingraPosDbContext context,
        string? userId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return null;

        var exists = await context.Users.AnyAsync(user => user.Id == userId, cancellationToken);

        return exists ? userId : null;
    }
}
