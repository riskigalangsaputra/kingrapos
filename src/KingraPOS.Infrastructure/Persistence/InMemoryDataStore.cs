using KingraPOS.Domain.Entities;

namespace KingraPOS.Infrastructure.Persistence;

public class InMemoryDataStore
{
    public InMemoryDataStore()
    {
        Seed();
    }

    public List<Category> Categories { get; } = new();

    public List<Product> Products { get; } = new();

    public List<Sale> Sales { get; } = new();

    private void Seed()
    {
        var makanan = new Category { Name = "Makanan", Description = "Produk makanan ringan" };
        var minuman = new Category { Name = "Minuman", Description = "Produk minuman" };

        Categories.AddRange(new[] { makanan, minuman });

        Products.AddRange(new[]
        {
            new Product { Sku = "MKN-001", Name = "Keripik Kentang", CategoryId = makanan.Id, Price = 12000m, Stock = 40 },
            new Product { Sku = "MKN-002", Name = "Biskuit Cokelat", CategoryId = makanan.Id, Price = 9500m, Stock = 60 },
            new Product { Sku = "MNM-001", Name = "Air Mineral 600ml", CategoryId = minuman.Id, Price = 4000m, Stock = 120 },
            new Product { Sku = "MNM-002", Name = "Teh Kemasan", CategoryId = minuman.Id, Price = 6000m, Stock = 80 }
        });
    }
}
