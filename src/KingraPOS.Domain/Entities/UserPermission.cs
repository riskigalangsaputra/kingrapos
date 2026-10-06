namespace KingraPOS.Domain.Entities;

public class UserPermission
{
    public string UserId { get; set; } = string.Empty;

    public string PermissionKey { get; set; } = string.Empty;

    public bool IsGranted { get; set; } = true;

    public string? GrantedByUserId { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }
}
