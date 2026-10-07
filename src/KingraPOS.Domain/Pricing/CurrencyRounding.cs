namespace KingraPOS.Domain.Pricing;

public static class CurrencyRounding
{
    /// <summary>
    /// Membulatkan nominal ke kelipatan terdekat sesuai pengaturan kas.
    /// Nilai <paramref name="cashRounding"/> 0 atau 1 berarti tanpa pembulatan.
    /// </summary>
    public static long Round(long amount, long cashRounding)
    {
        if (cashRounding <= 1)
            return amount;

        var rounded = Math.Round((decimal)amount / cashRounding, MidpointRounding.AwayFromZero) * cashRounding;

        return (long)rounded;
    }
}
