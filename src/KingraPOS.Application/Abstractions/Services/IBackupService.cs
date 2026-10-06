using KingraPOS.Application.Dtos;
using KingraPOS.Domain.Enums;

namespace KingraPOS.Application.Abstractions.Services;

public interface IBackupService
{
    Task<string> GetDefaultDestinationAsync(CancellationToken cancellationToken = default);

    Task<BackupScheduleDto?> GetScheduleAsync(CancellationToken cancellationToken = default);

    Task SaveScheduleAsync(BackupScheduleRequest request, CancellationToken cancellationToken = default);

    Task<BackupLogDto> RunBackupAsync(
        BackupTriggerType triggerType,
        string? userId = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BackupLogDto>> GetHistoryAsync(
        int take = 50,
        CancellationToken cancellationToken = default);

    Task<BackupHealthDto> GetHealthAsync(CancellationToken cancellationToken = default);

    Task<DateTimeOffset?> GetLastDueTimeAsync(CancellationToken cancellationToken = default);
}
