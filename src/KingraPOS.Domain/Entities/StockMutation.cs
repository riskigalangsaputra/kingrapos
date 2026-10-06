using KingraPOS.Domain.Common;
using KingraPOS.Domain.Enums;

namespace KingraPOS.Domain.Entities;

public class StockMutation : Entity
{
    public string ProductId { get; set; } = string.Empty;

    public string? ProductUnitId { get; set; }

    public string? SupplierId { get; set; }

    public string UserId { get; set; } = string.Empty;

    public StockMutationType MutationType { get; set; }

    public StockBucket StockBucket { get; set; } = StockBucket.AVAILABLE;

    public long Quantity { get; set; }

    public long? StockBefore { get; set; }

    public long? StockAfter { get; set; }

    public StockReferenceType? ReferenceType { get; set; }

    public string? ReferenceId { get; set; }

    public string? Notes { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
