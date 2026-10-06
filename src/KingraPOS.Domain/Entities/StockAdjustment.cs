using KingraPOS.Domain.Common;
using KingraPOS.Domain.Enums;

namespace KingraPOS.Domain.Entities;

public class StockAdjustment : Entity
{
    public string UserId { get; set; } = string.Empty;

    public StockAdjustmentReason Reason { get; set; } = StockAdjustmentReason.STOCK_COUNT;

    public string? SupplierId { get; set; }

    public string? SupplierContactId { get; set; }

    public string? ReceivedByUserId { get; set; }

    public string? ReferenceNumber { get; set; }

    public string? Notes { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
