namespace KingraPOS.Application.Abstractions.Services;

public interface IPermissionService
{
    Task<IReadOnlySet<string>> GetEffectivePermissionsAsync(
        string userId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlySet<string>> GetRolePermissionsAsync(
        string roleId,
        CancellationToken cancellationToken = default);

    bool HasPermission(string permissionKey);
}
