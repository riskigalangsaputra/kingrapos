using KingraPOS.Domain.Common;

namespace KingraPOS.Domain.Entities;

public class LoyaltyPointRule : Entity
{
    public long MinTransactionAmount { get; set; }

    public long AmountPerPoint { get; set; } = 10000;

    public long PointValueInCash { get; set; } = 100;

    public long MinRedeemPoints { get; set; }

    public double MaxRedeemPercent { get; set; } = 100.0;

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }
}
