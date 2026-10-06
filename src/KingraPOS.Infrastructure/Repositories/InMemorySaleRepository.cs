using KingraPOS.Domain.Entities;
using KingraPOS.Domain.Interfaces.Repositories;
using KingraPOS.Infrastructure.Persistence;

namespace KingraPOS.Infrastructure.Repositories;

public class InMemorySaleRepository : ISaleRepository
{
    private readonly InMemoryDataStore _store;

    public InMemorySaleRepository(InMemoryDataStore store)
    {
        _store = store;
    }

    public Task<IReadOnlyList<Sale>> GetAllAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Sale>>(_store.Sales.ToList());

    public Task<Sale?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_store.Sales.FirstOrDefault(sale => sale.Id == id));

    public Task<IReadOnlyList<Sale>> GetByDateAsync(DateTime date, CancellationToken cancellationToken = default)
    {
        var result = _store.Sales
            .Where(sale => sale.CreatedAt.Date == date.Date)
            .ToList();

        return Task.FromResult<IReadOnlyList<Sale>>(result);
    }

    public Task AddAsync(Sale entity, CancellationToken cancellationToken = default)
    {
        _store.Sales.Add(entity);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Sale entity, CancellationToken cancellationToken = default)
    {
        var index = _store.Sales.FindIndex(sale => sale.Id == entity.Id);
        if (index >= 0)
            _store.Sales[index] = entity;

        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _store.Sales.RemoveAll(sale => sale.Id == id);
        return Task.CompletedTask;
    }
}
