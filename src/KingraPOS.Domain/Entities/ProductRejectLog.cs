using KingraPOS.Domain.Common;
using KingraPOS.Domain.Enums;

namespace KingraPOS.Domain.Entities;

public class ProductRejectLog : Entity
{
    public string ProductId { get; set; } = string.Empty;

    public string UserId { get; set; } = string.Empty;

    public string RejectReason { get; set; } = string.Empty;

    public RejectStatus Status { get; set; } = RejectStatus.REJECTED_IN_STORE;

    public long Quantity { get; set; }

    public long CostPriceAtIncident { get; set; }

    public long TotalLossAmount { get; set; }

    public string? SupplierId { get; set; }

    public DateTimeOffset? ResolvedAt { get; set; }

    public string? ResolvedByUserId { get; set; }

    public string? Notes { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
