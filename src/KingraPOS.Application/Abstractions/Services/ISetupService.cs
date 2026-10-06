using KingraPOS.Application.Dtos;

namespace KingraPOS.Application.Abstractions.Services;

public interface ISetupService
{
    Task<bool> IsCompletedAsync(CancellationToken cancellationToken = default);

    Task<SetupStateDto> GetStateAsync(CancellationToken cancellationToken = default);

    Task CompleteAsync(SetupRequest request, CancellationToken cancellationToken = default);
}
