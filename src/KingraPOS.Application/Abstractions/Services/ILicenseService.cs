using KingraPOS.Application.Dtos;

namespace KingraPOS.Application.Abstractions.Services;

public interface ILicenseService
{
    Task<LicenseStateDto> GetStateAsync(CancellationToken cancellationToken = default);

    Task<LicenseStateDto> ActivateAsync(
        LicenseActivationRequest request,
        CancellationToken cancellationToken = default);

    Task<bool> IsWriteAllowedAsync(CancellationToken cancellationToken = default);

    Task EnsureWriteAllowedAsync(CancellationToken cancellationToken = default);
}
