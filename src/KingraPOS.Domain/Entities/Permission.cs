namespace KingraPOS.Domain.Entities;

public class Permission
{
    public string PermissionKey { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Module { get; set; } = string.Empty;

    public string? Description { get; set; }
}
