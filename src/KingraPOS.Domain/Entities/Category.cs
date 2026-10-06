using KingraPOS.Domain.Common;

namespace KingraPOS.Domain.Entities;

public class Category : Entity
{
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }
}
