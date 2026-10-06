namespace KingraPOS.Application.Abstractions.Security;

public interface IDeviceFingerprintProvider
{
    string GetFingerprint();
}
