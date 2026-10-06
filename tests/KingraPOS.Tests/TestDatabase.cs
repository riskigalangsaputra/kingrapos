using KingraPOS.Application.Abstractions.Persistence;
using KingraPOS.Application.Abstractions.Services;
using KingraPOS.Application.DependencyInjection;
using KingraPOS.Application.Dtos;
using KingraPOS.Domain.Entities;
using KingraPOS.Infrastructure.DependencyInjection;
using KingraPOS.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
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

    /// <summary>Menjalankan setup awal lalu login sebagai owner.</summary>
    public async Task InitializeAsync()
    {
        await GetRequiredService<ISetupService>().CompleteAsync(TestData.CreateSetupRequest());

        await GetRequiredService<IAuthenticationService>()
            .LoginAsync(new LoginRequest(TestData.OwnerUsername, TestData.OwnerPassword));
    }

    public async Task<string> SeedUnitAsync(string name, bool allowDecimal = false)
    {
        using var context = CreateContext();

        var existing = await context.Units
            .FirstOrDefaultAsync(unit => unit.DeletedAt == null && unit.Name == name);

        if (existing is not null)
            return existing.Id;

        var now = DateTimeOffset.UtcNow;

        var unit = new Unit
        {
            Name = name,
            AllowDecimal = allowDecimal,
            CreatedAt = now,
            UpdatedAt = now
        };

        context.Units.Add(unit);
        await context.SaveChangesAsync();

        return unit.Id;
    }

    public async Task<string> SeedProductAsync(
        string name,
        string sku,
        string baseUnitId,
        string? categoryId = null,
        long baseCostPrice = 0)
    {
        using var context = CreateContext();

        var now = DateTimeOffset.UtcNow;

        var product = new Product
        {
            Name = name,
            Sku = sku,
            BaseUnitId = baseUnitId,
            CategoryId = categoryId,
            BaseCostPrice = baseCostPrice,
            CreatedAt = now,
            UpdatedAt = now
        };

        context.Products.Add(product);
        await context.SaveChangesAsync();

        return product.Id;
    }

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
