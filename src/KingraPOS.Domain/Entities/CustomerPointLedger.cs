using KingraPOS.Domain.Common;
using KingraPOS.Domain.Enums;

namespace KingraPOS.Domain.Entities;

public class CustomerPointLedger : Entity
{
    public string CustomerId { get; set; } = string.Empty;

    public string UserId { get; set; } = string.Empty;

    public string? TransactionId { get; set; }

    public string? RedemptionId { get; set; }

    public PointLedgerEntryType EntryType { get; set; }

    public long Points { get; set; }

    public long? BalanceAfter { get; set; }

    public string? Notes { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
