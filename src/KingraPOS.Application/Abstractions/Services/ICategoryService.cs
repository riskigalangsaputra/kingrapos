using KingraPOS.Application.Dtos;

namespace KingraPOS.Application.Abstractions.Services;

public interface ICategoryService
{
    Task<IReadOnlyList<CategoryDto>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<CategoryDto> CreateAsync(CategoryRequest request, CancellationToken cancellationToken = default);

    Task<CategoryDto> UpdateAsync(
        string id,
        CategoryRequest request,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(string id, CancellationToken cancellationToken = default);
}
