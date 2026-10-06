using KingraPOS.Domain.Common;

namespace KingraPOS.Domain.Entities;

public class LoyaltyRewardItem : Entity
{
    public string ProductId { get; set; } = string.Empty;

    public string? ProductUnitId { get; set; }

    public string RewardName { get; set; } = string.Empty;

    public long PointsRequired { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }
}
