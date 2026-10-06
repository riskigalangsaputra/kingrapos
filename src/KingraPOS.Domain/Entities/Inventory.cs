using KingraPOS.Domain.Common;

namespace KingraPOS.Domain.Entities;

public class Inventory : Entity
{
    public string ProductId { get; set; } = string.Empty;

    public long StockQuantity { get; set; }

    public long RejectQuantity { get; set; }

    public long LowStockThreshold { get; set; } = 5000;

    public DateTimeOffset? LastMutationAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
