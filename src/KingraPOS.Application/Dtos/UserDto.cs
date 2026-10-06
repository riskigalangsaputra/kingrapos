namespace KingraPOS.Application.Dtos;

public record UserDto(
    string Id,
    string Name,
    string Username,
    string RoleName,
    bool IsActive,
    DateTimeOffset? LastLoginAt);

public record RoleDto(string Id, string Name, int LevelTier);

public record CreateUserRequest(
    string Name,
    string Username,
    string Password,
    string RoleId,
    string? EmployeeCode,
    bool IsActive = true);
