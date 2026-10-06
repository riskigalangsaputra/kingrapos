using KingraPOS.Application.Abstractions.Persistence;
using KingraPOS.Application.Abstractions.Security;
using KingraPOS.Infrastructure.Persistence;
using KingraPOS.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KingraPOS.Infrastructure.DependencyInjection;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string databasePath)
    {
        services.AddDbContextFactory<KingraPosDbContext>(options =>
            options.UseSqlite(KingraPosDatabase.BuildConnectionString(databasePath)));

        services.AddSingleton<IKingraPosDbContextFactory, KingraPosDbContextFactory>();
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton(new DatabaseInitializer(databasePath));

        return services;
    }
}
