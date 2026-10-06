using KingraPOS.Application.Dtos;

namespace KingraPOS.Application.Abstractions.Services;

public interface IRestoreService
{
    Task<RestoreInspectionDto> InspectAsync(string filePath, CancellationToken cancellationToken = default);

    Task<RestoreResultDto> RestoreAsync(
        string filePath,
        string? userId = null,
        CancellationToken cancellationToken = default);
}
