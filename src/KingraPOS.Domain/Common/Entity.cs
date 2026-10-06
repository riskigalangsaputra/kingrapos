namespace KingraPOS.Domain.Common;

public abstract class Entity
{
    public string Id { get; set; } = Guid.NewGuid().ToString("d");
}
