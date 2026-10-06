using KingraPOS.Application.Dtos;

namespace KingraPOS.Application.Abstractions.Services;

public interface IAuthenticationService
{
    Task<LoginResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);

    Task<bool> ChangePasswordAsync(
        string userId,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken = default);
}
