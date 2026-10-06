using KingraPOS.Application.Abstractions.Persistence;
using KingraPOS.Application.Abstractions.Security;
using KingraPOS.Application.Abstractions.Services;
using KingraPOS.Application.Services;
using KingraPOS.Infrastructure.BackgroundServices;
using KingraPOS.Infrastructure.Backup;
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
        services.AddSingleton<IDeviceFingerprintProvider, WindowsDeviceFingerprintProvider>();
        services.AddSingleton(new DatabaseInitializer(databasePath));

        services.AddSingleton<IDatabaseFileStore>(new SqliteDatabaseFileStore(databasePath));
        services.AddSingleton<IBackupService, BackupService>();
        services.AddSingleton<IRestoreService, RestoreService>();
        services.AddHostedService<BackupScheduler>();

        return services;
    }
}
