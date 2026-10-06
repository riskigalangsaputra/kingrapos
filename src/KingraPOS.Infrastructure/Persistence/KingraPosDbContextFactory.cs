using KingraPOS.Application.Abstractions.Persistence;
using Microsoft.EntityFrameworkCore;

namespace KingraPOS.Infrastructure.Persistence;

public sealed class KingraPosDbContextFactory : IKingraPosDbContextFactory
{
    private readonly IDbContextFactory<KingraPosDbContext> _contextFactory;

    public KingraPosDbContextFactory(IDbContextFactory<KingraPosDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public IKingraPosDbContext Create() => _contextFactory.CreateDbContext();
}
