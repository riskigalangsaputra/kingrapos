using KingraPOS.Domain.Common;
using KingraPOS.Domain.Enums;

namespace KingraPOS.Domain.Entities;

public class CustomerPointRedemption : Entity
{
    public string CustomerId { get; set; } = string.Empty;

    public string UserId { get; set; } = string.Empty;

    public string? TransactionId { get; set; }

    public string? RewardItemId { get; set; }

    public RedemptionType RedemptionType { get; set; }

    public long PointsRedeemed { get; set; }

    public long EquivalentCashValue { get; set; }

    public long QuantityGifted { get; set; } = 1;

    public string? Notes { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
