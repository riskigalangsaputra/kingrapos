using KingraPOS.Domain.Entities;

namespace KingraPOS.Domain.Interfaces.Repositories;

public interface IProductRepository : IRepository<Product>
{
    Task<Product?> GetBySkuAsync(string sku, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Product>> SearchAsync(string keyword, CancellationToken cancellationToken = default);
}
