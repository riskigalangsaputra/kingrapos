namespace KingraPOS.Domain.Entities;

public class AppMeta
{
    public string Key { get; set; } = string.Empty;

    public string? Value { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
