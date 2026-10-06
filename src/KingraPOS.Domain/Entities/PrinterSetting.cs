using KingraPOS.Domain.Common;
using KingraPOS.Domain.Enums;

namespace KingraPOS.Domain.Entities;

public class PrinterSetting : Entity
{
    public string Name { get; set; } = string.Empty;

    public PrinterType PrinterType { get; set; } = PrinterType.THERMAL;

    public PrinterConnectionType ConnectionType { get; set; } = PrinterConnectionType.USB;

    public string? Address { get; set; }

    public int PaperWidthMm { get; set; } = 80;

    public int Copies { get; set; } = 1;

    public bool AutoCut { get; set; } = true;

    public bool AutoOpenDrawer { get; set; }

    public bool IsDefault { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }
}
