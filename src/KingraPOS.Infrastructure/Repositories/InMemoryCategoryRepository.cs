using KingraPOS.Domain.Entities;
using KingraPOS.Domain.Interfaces.Repositories;
using KingraPOS.Infrastructure.Persistence;

namespace KingraPOS.Infrastructure.Repositories;

public class InMemoryCategoryRepository : ICategoryRepository
{
    private readonly InMemoryDataStore _store;

    public InMemoryCategoryRepository(InMemoryDataStore store)
    {
        _store = store;
    }

    public Task<IReadOnlyList<Category>> GetAllAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Category>>(_store.Categories.ToList());

    public Task<Category?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_store.Categories.FirstOrDefault(category => category.Id == id));

    public Task<Category?> GetByNameAsync(string name, CancellationToken cancellationToken = default) =>
        Task.FromResult(_store.Categories.FirstOrDefault(category =>
            string.Equals(category.Name, name, StringComparison.OrdinalIgnoreCase)));

    public Task AddAsync(Category entity, CancellationToken cancellationToken = default)
    {
        _store.Categories.Add(entity);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Category entity, CancellationToken cancellationToken = default)
    {
        var index = _store.Categories.FindIndex(category => category.Id == entity.Id);
        if (index >= 0)
            _store.Categories[index] = entity;

        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _store.Categories.RemoveAll(category => category.Id == id);
        return Task.CompletedTask;
    }
}
