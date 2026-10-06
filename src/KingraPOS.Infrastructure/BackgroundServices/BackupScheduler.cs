using KingraPOS.Application.Abstractions.Services;
using KingraPOS.Domain.Enums;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace KingraPOS.Infrastructure.BackgroundServices;

public sealed class BackupScheduler : BackgroundService
{
    private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan TickInterval = TimeSpan.FromMinutes(1);

    private readonly IBackupService _backupService;
    private readonly ILogger<BackupScheduler> _logger;

    public BackupScheduler(IBackupService backupService, ILogger<BackupScheduler> logger)
    {
        _backupService = backupService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(StartupDelay, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Pemeriksaan jadwal backup gagal.");
            }

            try
            {
                await Task.Delay(TickInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        var schedule = await _backupService.GetScheduleAsync(cancellationToken);
        if (schedule is null || !schedule.IsActive)
            return;

        var due = await _backupService.GetLastDueTimeAsync(cancellationToken);
        if (due is null)
            return;

        if (schedule.LastRunAt is not null && schedule.LastRunAt >= due)
            return;

        _logger.LogInformation("Menjalankan backup terjadwal (jatuh tempo {Due:u}).", due);

        var result = await _backupService.RunBackupAsync(BackupTriggerType.SCHEDULED, null, cancellationToken);

        if (result.Status != JobStatus.SUCCESS)
            _logger.LogWarning("Backup terjadwal gagal: {Error}", result.ErrorMessage);
    }
}
