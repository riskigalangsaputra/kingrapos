namespace KingraPOS.UI.Common;

public sealed record TimezoneOption(string DisplayName, string Timezone, int UtcOffsetMinutes)
{
    public static IReadOnlyList<TimezoneOption> All { get; } = new[]
    {
        new TimezoneOption("WIB — Asia/Jakarta (UTC+7)", "Asia/Jakarta", 420),
        new TimezoneOption("WITA — Asia/Makassar (UTC+8)", "Asia/Makassar", 480),
        new TimezoneOption("WIT — Asia/Jayapura (UTC+9)", "Asia/Jayapura", 540)
    };
}
