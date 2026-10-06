using KingraPOS.Domain.Entities;

namespace KingraPOS.Domain.Interfaces.Repositories;

public interface ISaleRepository : IRepository<Sale>
{
    Task<IReadOnlyList<Sale>> GetByDateAsync(DateTime date, CancellationToken cancellationToken = default);
}
