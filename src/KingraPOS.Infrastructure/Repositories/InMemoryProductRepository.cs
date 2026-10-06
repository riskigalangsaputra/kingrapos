using KingraPOS.Domain.Entities;
using KingraPOS.Domain.Interfaces.Repositories;
using KingraPOS.Infrastructure.Persistence;

namespace KingraPOS.Infrastructure.Repositories;

public class InMemoryProductRepository : IProductRepository
{
    private readonly InMemoryDataStore _store;

    public InMemoryProductRepository(InMemoryDataStore store)
    {
        _store = store;
    }

    public Task<IReadOnlyList<Product>> GetAllAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Product>>(_store.Products.ToList());

    public Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_store.Products.FirstOrDefault(product => product.Id == id));

    public Task<Product?> GetBySkuAsync(string sku, CancellationToken cancellationToken = default) =>
        Task.FromResult(_store.Products.FirstOrDefault(product =>
            string.Equals(product.Sku, sku, StringComparison.OrdinalIgnoreCase)));

    public Task<IReadOnlyList<Product>> SearchAsync(string keyword, CancellationToken cancellationToken = default)
    {
        var term = keyword?.Trim() ?? string.Empty;

        var result = string.IsNullOrEmpty(term)
            ? _store.Products.ToList()
            : _store.Products
                .Where(product =>
                    product.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    product.Sku.Contains(term, StringComparison.OrdinalIgnoreCase))
                .ToList();

        return Task.FromResult<IReadOnlyList<Product>>(result);
    }

    public Task AddAsync(Product entity, CancellationToken cancellationToken = default)
    {
        _store.Products.Add(entity);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Product entity, CancellationToken cancellationToken = default)
    {
        var index = _store.Products.FindIndex(product => product.Id == entity.Id);
        if (index >= 0)
            _store.Products[index] = entity;

        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _store.Products.RemoveAll(product => product.Id == id);
        return Task.CompletedTask;
    }
}
