using KingraPOS.Domain.Common;
using KingraPOS.Domain.Enums;

namespace KingraPOS.Domain.Entities;

public class ProductPriceChangelog : Entity
{
    public string? ProductId { get; set; }

    public string? ProductUnitId { get; set; }

    public string UserId { get; set; } = string.Empty;

    public PriceChangeType PriceType { get; set; }

    public long OldPrice { get; set; }

    public long NewPrice { get; set; }

    public string? Notes { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
