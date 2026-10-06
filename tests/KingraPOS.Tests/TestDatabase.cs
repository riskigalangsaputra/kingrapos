using KingraPOS.Application.Abstractions.Persistence;
using KingraPOS.Application.DependencyInjection;
using KingraPOS.Infrastructure.DependencyInjection;
using KingraPOS.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace KingraPOS.Tests;

internal sealed class TestDatabase : IDisposable
{
    private readonly ServiceProvider _serviceProvider;

    public TestDatabase()
    {
        DatabasePath = Path.Combine(
            Path.GetTempPath(),
            $"kingrapos-test-{Guid.NewGuid():N}.db");

        DatabaseInitializer.Initialize(DatabasePath);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication();
        services.AddInfrastructure(DatabasePath);

        _serviceProvider = services.BuildServiceProvider();
    }

    public string DatabasePath { get; }

    public T GetRequiredService<T>() where T : notnull =>
        _serviceProvider.GetRequiredService<T>();

    public IKingraPosDbContext CreateContext() =>
        GetRequiredService<IKingraPosDbContextFactory>().Create();

    public void Dispose()
    {
        _serviceProvider.Dispose();
        SqliteConnection.ClearAllPools();

        foreach (var file in new[] { DatabasePath, DatabasePath + "-wal", DatabasePath + "-shm" })
        {
            try
            {
                if (File.Exists(file))
                    File.Delete(file);
            }
            catch (IOException)
            {
                // file sementara gagal dihapus — biarkan OS membersihkan
            }
        }
    }
}
