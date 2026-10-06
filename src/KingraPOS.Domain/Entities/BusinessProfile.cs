using KingraPOS.Domain.Common;

namespace KingraPOS.Domain.Entities;

public class BusinessProfile : Entity
{
    public string Name { get; set; } = string.Empty;

    public string OwnerName { get; set; } = string.Empty;

    public string? PhoneNumber { get; set; }

    public string? Address { get; set; }

    public string? TaxNumber { get; set; }

    public string? ReceiptFooter { get; set; }

    public string? LogoPath { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
