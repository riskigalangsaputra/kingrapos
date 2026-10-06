using KingraPOS.Domain.Common;

namespace KingraPOS.Domain.Entities;

public class ProductPriceTier : Entity
{
    public string ProductPriceId { get; set; } = string.Empty;

    public string TierName { get; set; } = string.Empty;

    public long MinimumQuantity { get; set; } = 1000;

    public long TierPrice { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }
}
