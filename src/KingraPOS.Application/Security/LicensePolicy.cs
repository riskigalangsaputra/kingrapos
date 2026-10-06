namespace KingraPOS.Application.Security;

public static class LicensePolicy
{
    public const int TrialDays = 30;

    public static TimeSpan GracePeriod { get; } = TimeSpan.FromDays(14);
}
