using KingraPOS.Domain.Common;

namespace KingraPOS.Domain.Entities;

public class SupplierContact : Entity
{
    public string SupplierId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? PhoneNumber { get; set; }

    public string? Position { get; set; }

    public bool IsPrimary { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }
}
