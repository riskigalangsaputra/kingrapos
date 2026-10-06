using KingraPOS.Domain.Common;

namespace KingraPOS.Domain.Entities;

public class Customer : Entity
{
    public string? MemberCode { get; set; }

    public string Name { get; set; } = string.Empty;

    public string PhoneNumber { get; set; } = string.Empty;

    public string? Email { get; set; }

    public string? Address { get; set; }

    public string? Notes { get; set; }

    public long LoyaltyPoints { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }
}
