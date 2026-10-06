using KingraPOS.Domain.Common;

namespace KingraPOS.Domain.Entities;

public class StockAdjustmentItem : Entity
{
    public string StockAdjustmentId { get; set; } = string.Empty;

    public string ProductId { get; set; } = string.Empty;

    public string? ProductUnitId { get; set; }

    public long? QuantityInput { get; set; }

    public long QuantityBefore { get; set; }

    public long QuantityAfter { get; set; }

    public long Difference { get; set; }

    public long? CostPrice { get; set; }
}
