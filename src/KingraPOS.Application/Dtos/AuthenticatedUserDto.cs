namespace KingraPOS.Application.Dtos;

public record AuthenticatedUserDto(
    string UserId,
    string Name,
    string Username,
    string RoleId,
    string RoleName,
    string? EmployeeCode);
