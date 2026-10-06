namespace KingraPOS.Application.Abstractions.Security;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
