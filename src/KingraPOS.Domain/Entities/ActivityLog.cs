using KingraPOS.Domain.Common;

namespace KingraPOS.Domain.Entities;

public class ActivityLog : Entity
{
    public string? UserId { get; set; }

    public string ActionType { get; set; } = string.Empty;

    public string? EntityType { get; set; }

    public string? EntityId { get; set; }

    public string Description { get; set; } = string.Empty;

    public string? Payload { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
