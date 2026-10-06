using KingraPOS.Domain.Common;

namespace KingraPOS.Domain.Entities;

public class Product : Entity
{
    public string Sku { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public Guid CategoryId { get; set; }

    public decimal Price { get; set; }

    public int Stock { get; set; }

    public bool HasStock(int quantity) => quantity > 0 && Stock >= quantity;

    public void ReduceStock(int quantity)
    {
        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity), "Jumlah harus lebih dari nol.");

        if (!HasStock(quantity))
            throw new InvalidOperationException($"Stok produk '{Name}' tidak mencukupi.");

        Stock -= quantity;
    }
}
