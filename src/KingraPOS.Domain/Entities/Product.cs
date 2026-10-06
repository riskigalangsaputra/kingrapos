using KingraPOS.Domain.Common;

namespace KingraPOS.Domain.Entities;

public class Product : Entity
{
    public string? CategoryId { get; set; }

    public string BaseUnitId { get; set; } = string.Empty;

    public string Sku { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? ImagePath { get; set; }

    public long BaseCostPrice { get; set; }

    public long? DefaultServiceFee { get; set; }

    public bool TrackStock { get; set; } = true;

    public bool IsNewProduct { get; set; } = true;

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }
}
