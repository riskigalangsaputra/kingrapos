using KingraPOS.Application.Dtos;

namespace KingraPOS.Application.Abstractions.Services;

public interface IUnitService
{
    Task<IReadOnlyList<UnitDto>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<UnitDto> CreateAsync(UnitRequest request, CancellationToken cancellationToken = default);

    Task<UnitDto> UpdateAsync(string id, UnitRequest request, CancellationToken cancellationToken = default);

    Task DeleteAsync(string id, CancellationToken cancellationToken = default);
}
