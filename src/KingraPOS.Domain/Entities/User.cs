using KingraPOS.Domain.Common;

namespace KingraPOS.Domain.Entities;

public class User : Entity
{
    public string RoleId { get; set; } = string.Empty;

    public string? EmployeeCode { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Username { get; set; } = string.Empty;

    public string? Email { get; set; }

    public string PasswordHash { get; set; } = string.Empty;

    public string? PinHash { get; set; }

    public string? PhoneNumber { get; set; }

    public string? Address { get; set; }

    public string? PhotoPath { get; set; }

    public DateOnly? JoinDate { get; set; }

    public string? Notes { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTimeOffset? LastLoginAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }
}
