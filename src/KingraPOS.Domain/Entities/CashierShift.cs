using KingraPOS.Domain.Common;
using KingraPOS.Domain.Enums;

namespace KingraPOS.Domain.Entities;

public class CashierShift : Entity
{
    public string UserId { get; set; } = string.Empty;

    public DateTimeOffset StartTime { get; set; }

    public DateTimeOffset? EndTime { get; set; }

    public long CashDrawerStart { get; set; }

    public long? CashDrawerEndSystem { get; set; }

    public long? CashDrawerEndPhysical { get; set; }

    public long? DiscrepancyAmount { get; set; }

    public ShiftStatus Status { get; set; } = ShiftStatus.OPEN;

    public string? Notes { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
