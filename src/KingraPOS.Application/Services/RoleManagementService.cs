using KingraPOS.Application.Abstractions.Persistence;
using KingraPOS.Application.Abstractions.Security;
using KingraPOS.Application.Abstractions.Services;
using KingraPOS.Application.Dtos;
using KingraPOS.Application.Security;
using KingraPOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace KingraPOS.Application.Services;

public sealed class RoleManagementService : IRoleManagementService
{
    private readonly IKingraPosDbContextFactory _contextFactory;
    private readonly IPermissionService _permissionService;
    private readonly ICurrentUserSession _session;
    private readonly IActivityLogService _activityLogService;
    private readonly ILicenseService _licenseService;
    private readonly IClock _clock;

    public RoleManagementService(
        IKingraPosDbContextFactory contextFactory,
        IPermissionService permissionService,
        ICurrentUserSession session,
        IActivityLogService activityLogService,
        ILicenseService licenseService,
        IClock clock)
    {
        _contextFactory = contextFactory;
        _permissionService = permissionService;
        _session = session;
        _activityLogService = activityLogService;
        _licenseService = licenseService;
        _clock = clock;
    }

    public async Task<IReadOnlyList<RoleDto>> GetRolesAsync(CancellationToken cancellationToken = default)
    {
        using var context = _contextFactory.Create();

        return await context.Roles
            .Where(role => role.DeletedAt == null)
            .OrderBy(role => role.LevelTier)
            .Select(role => new RoleDto(role.Id, role.Name, role.LevelTier))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PermissionDto>> GetPermissionsAsync(CancellationToken cancellationToken = default)
    {
        using var context = _contextFactory.Create();

        return await context.Permissions
            .OrderBy(permission => permission.Module)
            .ThenBy(permission => permission.PermissionKey)
            .Select(permission => new PermissionDto(
                permission.PermissionKey,
                permission.Name,
                permission.Module))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<string>> GetRolePermissionKeysAsync(
        string roleId,
        CancellationToken cancellationToken = default)
    {
        using var context = _contextFactory.Create();

        return await context.RolePermissions
            .Where(permission => permission.RoleId == roleId && permission.DeletedAt == null)
            .Select(permission => permission.PermissionKey)
            .ToListAsync(cancellationToken);
    }

    public async Task SetRolePermissionsAsync(
        string roleId,
        IReadOnlyCollection<string> permissionKeys,
        CancellationToken cancellationToken = default)
    {
        EnsurePermission(PermissionCatalog.RoleManage);
        await _licenseService.EnsureWriteAllowedAsync(cancellationToken);

        using var context = _contextFactory.Create();

        var role = await context.Roles
            .FirstOrDefaultAsync(row => row.Id == roleId && row.DeletedAt == null, cancellationToken)
            ?? throw new InvalidOperationException("Role tidak ditemukan.");

        var validKeys = await context.Permissions
            .Select(permission => permission.PermissionKey)
            .ToListAsync(cancellationToken);

        var validSet = new HashSet<string>(validKeys, StringComparer.OrdinalIgnoreCase);
        var desired = new HashSet<string>(
            permissionKeys.Where(validSet.Contains),
            StringComparer.OrdinalIgnoreCase);

        var existing = await context.RolePermissions
            .Where(permission => permission.RoleId == roleId)
            .ToListAsync(cancellationToken);

        // role_permissions memakai primary key gabungan (role_id, permission_key), sehingga baris
        // yang tidak lagi dipakai dihapus permanen — soft delete akan menabrak primary key saat ditambah ulang.
        foreach (var row in existing.Where(row => !desired.Contains(row.PermissionKey)))
            context.RolePermissions.Remove(row);

        var existingKeys = new HashSet<string>(
            existing.Select(row => row.PermissionKey),
            StringComparer.OrdinalIgnoreCase);

        var now = _clock.UtcNow;

        foreach (var permissionKey in desired.Where(key => !existingKeys.Contains(key)))
        {
            context.RolePermissions.Add(new RolePermission
            {
                RoleId = roleId,
                PermissionKey = permissionKey,
                UpdatedAt = now
            });
        }

        await context.SaveChangesAsync(cancellationToken);

        await _activityLogService.LogAsync(
            "ROLE_PERMISSIONS_UPDATED",
            $"Izin role '{role.Name}' diperbarui menjadi {desired.Count} izin.",
            userId: _session.User?.UserId,
            entityType: "roles",
            entityId: roleId,
            cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<UserOverrideDto>> GetUserOverridesAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        using var context = _contextFactory.Create();

        var rows = await context.UserPermissions
            .Where(permission => permission.UserId == userId && permission.DeletedAt == null)
            .Select(permission => new { permission.PermissionKey, permission.IsGranted })
            .ToListAsync(cancellationToken);

        return rows
            .Select(row => new UserOverrideDto(
                row.PermissionKey,
                row.IsGranted ? PermissionOverrideState.Allow : PermissionOverrideState.Deny))
            .ToList();
    }

    public async Task SetUserOverrideAsync(
        string userId,
        string permissionKey,
        PermissionOverrideState state,
        CancellationToken cancellationToken = default)
    {
        EnsurePermission(PermissionCatalog.RoleManage);
        await _licenseService.EnsureWriteAllowedAsync(cancellationToken);

        using var context = _contextFactory.Create();

        var user = await context.Users
            .FirstOrDefaultAsync(row => row.Id == userId && row.DeletedAt == null, cancellationToken)
            ?? throw new InvalidOperationException("User tidak ditemukan.");

        var permissionExists = await context.Permissions
            .AnyAsync(row => row.PermissionKey == permissionKey, cancellationToken);

        if (!permissionExists)
            throw new InvalidOperationException($"Izin '{permissionKey}' tidak dikenal.");

        var existing = await context.UserPermissions
            .FirstOrDefaultAsync(
                row => row.UserId == userId && row.PermissionKey == permissionKey,
                cancellationToken);

        var now = _clock.UtcNow;

        if (state == PermissionOverrideState.Default)
        {
            if (existing is not null)
                context.UserPermissions.Remove(existing);
        }
        else if (existing is null)
        {
            context.UserPermissions.Add(new UserPermission
            {
                UserId = userId,
                PermissionKey = permissionKey,
                IsGranted = state == PermissionOverrideState.Allow,
                GrantedByUserId = _session.User?.UserId,
                UpdatedAt = now
            });
        }
        else
        {
            existing.IsGranted = state == PermissionOverrideState.Allow;
            existing.GrantedByUserId = _session.User?.UserId;
            existing.UpdatedAt = now;
        }

        await context.SaveChangesAsync(cancellationToken);

        await _activityLogService.LogAsync(
            "USER_PERMISSION_OVERRIDE_UPDATED",
            $"Override izin '{permissionKey}' untuk user '{user.Username}' diubah menjadi {state}.",
            userId: _session.User?.UserId,
            entityType: "users",
            entityId: userId,
            cancellationToken: cancellationToken);
    }

    private void EnsurePermission(string permissionKey)
    {
        if (!_permissionService.HasPermission(permissionKey))
            throw new UnauthorizedAccessException($"Anda tidak memiliki izin '{permissionKey}'.");
    }
}
