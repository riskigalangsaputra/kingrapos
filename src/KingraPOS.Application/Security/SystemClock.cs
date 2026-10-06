using KingraPOS.Application.Abstractions.Security;

namespace KingraPOS.Application.Security;

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
