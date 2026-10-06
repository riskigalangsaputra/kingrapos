using KingraPOS.Domain.Common;
using KingraPOS.Domain.Enums;

namespace KingraPOS.Domain.Entities;

public class ProductPrice : Entity
{
    public string ProductUnitId { get; set; } = string.Empty;

    public long SellingPrice { get; set; }

    public PriceTrend PriceTrend { get; set; } = PriceTrend.STABLE;

    public long? PreviousSellingPrice { get; set; }

    public DateTimeOffset? PriceChangedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }
}
