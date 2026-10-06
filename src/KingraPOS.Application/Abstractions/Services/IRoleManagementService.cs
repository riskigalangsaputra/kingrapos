using KingraPOS.Application.Dtos;

namespace KingraPOS.Application.Abstractions.Services;

public interface IRoleManagementService
{
    Task<IReadOnlyList<RoleDto>> GetRolesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PermissionDto>> GetPermissionsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetRolePermissionKeysAsync(
        string roleId,
        CancellationToken cancellationToken = default);

    Task SetRolePermissionsAsync(
        string roleId,
        IReadOnlyCollection<string> permissionKeys,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UserOverrideDto>> GetUserOverridesAsync(
        string userId,
        CancellationToken cancellationToken = default);

    Task SetUserOverrideAsync(
        string userId,
        string permissionKey,
        PermissionOverrideState state,
        CancellationToken cancellationToken = default);
}
