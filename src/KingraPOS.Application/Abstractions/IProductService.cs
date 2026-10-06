using KingraPOS.Application.Dtos;

namespace KingraPOS.Application.Abstractions;

public interface IProductService
{
    Task<IReadOnlyList<ProductDto>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProductDto>> SearchAsync(string keyword, CancellationToken cancellationToken = default);

    Task<ProductDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<ProductDto> CreateAsync(
        string sku,
        string name,
        Guid categoryId,
        decimal price,
        int stock,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(ProductDto product, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
