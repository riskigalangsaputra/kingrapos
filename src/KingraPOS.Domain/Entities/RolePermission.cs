namespace KingraPOS.Domain.Entities;

public class RolePermission
{
    public string RoleId { get; set; } = string.Empty;

    public string PermissionKey { get; set; } = string.Empty;

    public DateTimeOffset UpdatedAt { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }
}
