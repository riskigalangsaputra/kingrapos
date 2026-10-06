using KingraPOS.Application.Abstractions.Persistence;
using KingraPOS.Application.Abstractions.Services;
using Microsoft.EntityFrameworkCore;

namespace KingraPOS.Application.Services;

public sealed class PermissionService : IPermissionService
{
    private readonly IKingraPosDbContextFactory _contextFactory;
    private readonly ICurrentUserSession _session;

    public PermissionService(IKingraPosDbContextFactory contextFactory, ICurrentUserSession session)
    {
        _contextFactory = contextFactory;
        _session = session;
    }

    public async Task<IReadOnlySet<string>> GetEffectivePermissionsAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        using var context = _contextFactory.Create();

        var roleId = await context.Users
            .Where(user => user.Id == userId)
            .Select(user => user.RoleId)
            .FirstOrDefaultAsync(cancellationToken);

        var effective = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrEmpty(roleId))
            effective.UnionWith(await GetRolePermissionsAsync(roleId, cancellationToken));

        var overrides = await context.UserPermissions
            .Where(permission => permission.UserId == userId && permission.DeletedAt == null)
            .Select(permission => new { permission.PermissionKey, permission.IsGranted })
            .ToListAsync(cancellationToken);

        foreach (var item in overrides)
        {
            if (item.IsGranted)
                effective.Add(item.PermissionKey);
            else
                effective.Remove(item.PermissionKey);
        }

        return effective;
    }

    public async Task<IReadOnlySet<string>> GetRolePermissionsAsync(
        string roleId,
        CancellationToken cancellationToken = default)
    {
        using var context = _contextFactory.Create();

        var keys = await context.RolePermissions
            .Where(permission => permission.RoleId == roleId && permission.DeletedAt == null)
            .Select(permission => permission.PermissionKey)
            .ToListAsync(cancellationToken);

        return new HashSet<string>(keys, StringComparer.OrdinalIgnoreCase);
    }

    public bool HasPermission(string permissionKey) => _session.Permissions.Contains(permissionKey);
}
