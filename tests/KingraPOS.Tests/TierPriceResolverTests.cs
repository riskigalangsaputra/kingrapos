using KingraPOS.Domain.Entities;
using KingraPOS.Domain.Pricing;

namespace KingraPOS.Tests;

public class TierPriceResolverTests
{
    private static ProductPriceTier Tier(long minimumQuantity, long tierPrice, string name = "Tier") => new()
    {
        TierName = name,
        MinimumQuantity = minimumQuantity,
        TierPrice = tierPrice
    };

    private static readonly ProductPriceTier[] Tiers =
    {
        Tier(10_000, 9_000, "Grosir"),
        Tier(100_000, 8_000, "Bal")
    };

    [Fact]
    public void Without_tiers_the_base_price_is_used()
    {
        Assert.Equal(10_000, TierPriceResolver.Resolve(10_000, Array.Empty<ProductPriceTier>(), 50_000));
    }

    [Fact]
    public void Below_the_lowest_tier_uses_the_base_price()
    {
        Assert.Equal(10_000, TierPriceResolver.Resolve(10_000, Tiers, 9_999));
    }

    [Fact]
    public void Exactly_at_a_tier_boundary_uses_that_tier()
    {
        Assert.Equal(9_000, TierPriceResolver.Resolve(10_000, Tiers, 10_000));
        Assert.Equal(8_000, TierPriceResolver.Resolve(10_000, Tiers, 100_000));
    }

    [Fact]
    public void Between_tiers_uses_the_lower_tier()
    {
        Assert.Equal(9_000, TierPriceResolver.Resolve(10_000, Tiers, 99_999));
    }

    [Fact]
    public void Above_the_highest_tier_uses_the_highest_tier()
    {
        Assert.Equal(8_000, TierPriceResolver.Resolve(10_000, Tiers, 5_000_000));
    }

    [Fact]
    public void Zero_or_negative_quantity_uses_the_base_price()
    {
        Assert.Equal(10_000, TierPriceResolver.Resolve(10_000, Tiers, 0));
        Assert.Equal(10_000, TierPriceResolver.Resolve(10_000, Tiers, -500));
    }
}
