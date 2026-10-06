using KingraPOS.Domain.Entities;

namespace KingraPOS.Domain.Pricing;

public static class TierPriceResolver
{
    /// <summary>
    /// Memilih harga bertingkat: tier dengan minimum_quantity terbesar yang masih &lt;= quantity.
    /// Di bawah tier terendah (atau tanpa tier) memakai harga dasar.
    /// </summary>
    public static long Resolve(long basePrice, IEnumerable<ProductPriceTier> tiers, long quantity)
    {
        ProductPriceTier? selected = null;

        foreach (var tier in tiers)
        {
            if (tier.MinimumQuantity > quantity)
                continue;

            if (selected is null || tier.MinimumQuantity > selected.MinimumQuantity)
                selected = tier;
        }

        return selected?.TierPrice ?? basePrice;
    }
}
