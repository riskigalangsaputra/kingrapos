using KingraPOS.Application.Dtos;

namespace KingraPOS.Application.Abstractions.Services;

public interface IShiftService
{
    Task<ShiftDto?> GetOpenShiftAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ShiftDto>> GetRecentAsync(int take = 50, CancellationToken cancellationToken = default);

    Task<ShiftDto> OpenAsync(OpenShiftRequest request, CancellationToken cancellationToken = default);

    Task<ShiftDto> CloseAsync(CloseShiftRequest request, CancellationToken cancellationToken = default);

    Task<ShiftDto> SwitchAsync(SwitchShiftRequest request, CancellationToken cancellationToken = default);
}
