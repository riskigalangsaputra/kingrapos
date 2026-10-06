using KingraPOS.Application.Abstractions.Persistence;
using KingraPOS.Application.Abstractions.Security;
using KingraPOS.Application.Abstractions.Services;
using KingraPOS.Application.Dtos;
using Microsoft.EntityFrameworkCore;

namespace KingraPOS.Application.Services;

public sealed class AuthenticationService : IAuthenticationService
{
    private readonly IKingraPosDbContextFactory _contextFactory;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IPermissionService _permissionService;
    private readonly ICurrentUserSession _session;
    private readonly IActivityLogService _activityLogService;
    private readonly IClock _clock;

    public AuthenticationService(
        IKingraPosDbContextFactory contextFactory,
        IPasswordHasher passwordHasher,
        IPermissionService permissionService,
        ICurrentUserSession session,
        IActivityLogService activityLogService,
        IClock clock)
    {
        _contextFactory = contextFactory;
        _passwordHasher = passwordHasher;
        _permissionService = permissionService;
        _session = session;
        _activityLogService = activityLogService;
        _clock = clock;
    }

    public async Task<LoginResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        using var context = _contextFactory.Create();

        var username = request.Username.Trim();

        var user = await context.Users.FirstOrDefaultAsync(
            row => row.Username == username && row.DeletedAt == null && row.IsActive,
            cancellationToken);

        if (user is null || !_passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            await _activityLogService.LogAsync(
                "LOGIN_FAILED",
                $"Login gagal untuk username '{username}'.",
                userId: user?.Id,
                cancellationToken: cancellationToken);

            return LoginResult.Failed("Username atau password salah.");
        }

        var roleName = await context.Roles
            .Where(role => role.Id == user.RoleId)
            .Select(role => role.Name)
            .FirstOrDefaultAsync(cancellationToken) ?? "-";

        var permissions = await _permissionService.GetEffectivePermissionsAsync(user.Id, cancellationToken);

        var authenticatedUser = new AuthenticatedUserDto(
            user.Id,
            user.Name,
            user.Username,
            user.RoleId,
            roleName,
            user.EmployeeCode);

        _session.SignIn(authenticatedUser, permissions.ToArray());

        user.LastLoginAt = _clock.UtcNow;
        await context.SaveChangesAsync(cancellationToken);

        await _activityLogService.LogAsync(
            "LOGIN_SUCCESS",
            $"User '{username}' berhasil login.",
            userId: user.Id,
            cancellationToken: cancellationToken);

        return LoginResult.Ok(authenticatedUser);
    }

    public async Task<bool> ChangePasswordAsync(
        string userId,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        using var context = _contextFactory.Create();

        var user = await context.Users.FirstOrDefaultAsync(row => row.Id == userId, cancellationToken);
        if (user is null || !_passwordHasher.Verify(currentPassword, user.PasswordHash))
            return false;

        user.PasswordHash = _passwordHasher.Hash(newPassword);
        user.UpdatedAt = _clock.UtcNow;
        await context.SaveChangesAsync(cancellationToken);

        await _activityLogService.LogAsync(
            "PASSWORD_CHANGED",
            $"Password user '{user.Username}' diubah.",
            userId: user.Id,
            entityType: "users",
            entityId: user.Id,
            cancellationToken: cancellationToken);

        return true;
    }
}
