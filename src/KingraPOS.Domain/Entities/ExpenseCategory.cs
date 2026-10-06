using KingraPOS.Domain.Common;

namespace KingraPOS.Domain.Entities;

public class ExpenseCategory : Entity
{
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsSystem { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }
}
