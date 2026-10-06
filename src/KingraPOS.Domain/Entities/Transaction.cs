using KingraPOS.Domain.Common;
using KingraPOS.Domain.Enums;

namespace KingraPOS.Domain.Entities;

public class Transaction : Entity
{
    public string UserId { get; set; } = string.Empty;

    public string? CustomerId { get; set; }

    public string ShiftId { get; set; } = string.Empty;

    public string InvoiceNumber { get; set; } = string.Empty;

    public long TotalCostPrice { get; set; }

    public long ItemsAmount { get; set; }

    public long ServiceAmount { get; set; }

    public long TotalGrossAmount { get; set; }

    public long DiscountAmount { get; set; }

    public long PointDiscountAmount { get; set; }

    public double TaxRate { get; set; }

    public bool TaxInclusive { get; set; }

    public long TaxAmount { get; set; }

    public long RoundingAmount { get; set; }

    public long TotalNetAmount { get; set; }

    public string PaymentMethod { get; set; } = string.Empty;

    public string? PaymentProvider { get; set; }

    public string? PaymentReference { get; set; }

    public long AmountPaid { get; set; }

    public long AmountChange { get; set; }

    public long PointsEarned { get; set; }

    public long PointsUsed { get; set; }

    public TransactionStatus Status { get; set; } = TransactionStatus.COMPLETED;

    public DateTimeOffset? VoidedAt { get; set; }

    public string? VoidedByUserId { get; set; }

    public string? VoidReason { get; set; }

    public string? Notes { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
