using KingraPOS.Application.Abstractions.Persistence;
using KingraPOS.Application.Abstractions.Security;
using KingraPOS.Application.Abstractions.Services;
using KingraPOS.Application.Dtos;
using KingraPOS.Application.Security;
using KingraPOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace KingraPOS.Application.Services;

public sealed class UserManagementService : IUserManagementService
{
    private readonly IKingraPosDbContextFactory _contextFactory;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IPermissionService _permissionService;
    private readonly ICurrentUserSession _session;
    private readonly IActivityLogService _activityLogService;
    private readonly ILicenseService _licenseService;
    private readonly IClock _clock;

    public UserManagementService(
        IKingraPosDbContextFactory contextFactory,
        IPasswordHasher passwordHasher,
        IPermissionService permissionService,
        ICurrentUserSession session,
        IActivityLogService activityLogService,
        ILicenseService licenseService,
        IClock clock)
    {
        _contextFactory = contextFactory;
        _passwordHasher = passwordHasher;
        _permissionService = permissionService;
        _session = session;
        _activityLogService = activityLogService;
        _licenseService = licenseService;
        _clock = clock;
    }

    public async Task<IReadOnlyList<UserDto>> GetUsersAsync(CancellationToken cancellationToken = default)
    {
        using var context = _contextFactory.Create();

        var users = await context.Users
            .Where(user => user.DeletedAt == null)
            .OrderBy(user => user.Name)
            .ToListAsync(cancellationToken);

        var roleNames = await context.Roles
            .ToDictionaryAsync(role => role.Id, role => role.Name, cancellationToken);

        return users
            .Select(user => new UserDto(
                user.Id,
                user.Name,
                user.Username,
                roleNames.TryGetValue(user.RoleId, out var roleName) ? roleName : "-",
                user.IsActive,
                user.LastLoginAt))
            .ToList();
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

    public async Task<UserDto> CreateUserAsync(CreateUserRequest request, CancellationToken cancellationToken = default)
    {
        EnsurePermission(PermissionCatalog.UserManage);
        await _licenseService.EnsureWriteAllowedAsync(cancellationToken);

        using var context = _contextFactory.Create();

        var username = request.Username.Trim();

        var usernameTaken = await context.Users
            .AnyAsync(user => user.Username == username && user.DeletedAt == null, cancellationToken);

        if (usernameTaken)
            throw new InvalidOperationException($"Username '{username}' sudah dipakai.");

        var role = await context.Roles
            .FirstOrDefaultAsync(row => row.Id == request.RoleId && row.DeletedAt == null, cancellationToken)
            ?? throw new InvalidOperationException("Role yang dipilih tidak ditemukan.");

        var now = _clock.UtcNow;

        var user = new User
        {
            RoleId = role.Id,
            Name = request.Name.Trim(),
            Username = username,
            EmployeeCode = request.EmployeeCode,
            PasswordHash = _passwordHasher.Hash(request.Password),
            IsActive = request.IsActive,
            CreatedAt = now,
            UpdatedAt = now
        };

        context.Users.Add(user);
        await context.SaveChangesAsync(cancellationToken);

        await _activityLogService.LogAsync(
            "USER_CREATED",
            $"User '{username}' dibuat dengan role {role.Name}.",
            userId: _session.User?.UserId,
            entityType: "users",
            entityId: user.Id,
            cancellationToken: cancellationToken);

        return new UserDto(user.Id, user.Name, user.Username, role.Name, user.IsActive, null);
    }

    public async Task SetUserActiveAsync(string userId, bool isActive, CancellationToken cancellationToken = default)
    {
        EnsurePermission(PermissionCatalog.UserManage);
        await _licenseService.EnsureWriteAllowedAsync(cancellationToken);

        using var context = _contextFactory.Create();

        var user = await context.Users.FirstOrDefaultAsync(row => row.Id == userId, cancellationToken)
            ?? throw new InvalidOperationException("User tidak ditemukan.");

        if (!isActive && _session.User?.UserId == userId)
            throw new InvalidOperationException("Tidak dapat menonaktifkan akun yang sedang login.");

        user.IsActive = isActive;
        user.UpdatedAt = _clock.UtcNow;
        await context.SaveChangesAsync(cancellationToken);

        await _activityLogService.LogAsync(
            isActive ? "USER_ACTIVATED" : "USER_DEACTIVATED",
            $"User '{user.Username}' {(isActive ? "diaktifkan" : "dinonaktifkan")}.",
            userId: _session.User?.UserId,
            entityType: "users",
            entityId: user.Id,
            cancellationToken: cancellationToken);
    }

    private void EnsurePermission(string permissionKey)
    {
        if (!_permissionService.HasPermission(permissionKey))
            throw new UnauthorizedAccessException($"Anda tidak memiliki izin '{permissionKey}'.");
    }
}
