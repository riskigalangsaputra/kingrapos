namespace KingraPOS.Application.Dtos;

public record LoginRequest(string Username, string Password);

public record LoginResult(bool Success, AuthenticatedUserDto? User, string? ErrorMessage)
{
    public static LoginResult Failed(string message) => new(false, null, message);

    public static LoginResult Ok(AuthenticatedUserDto user) => new(true, user, null);
}
