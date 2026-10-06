using KingraPOS.Domain.Common;

namespace KingraPOS.Domain.Entities;

public class ProductUnit : Entity
{
    public string ProductId { get; set; } = string.Empty;

    public string UnitId { get; set; } = string.Empty;

    public string? Barcode { get; set; }

    public long ConversionFactor { get; set; } = 1000;

    public bool IsBaseUnit { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }
}
