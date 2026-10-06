using KingraPOS.Domain.Inventory;

namespace KingraPOS.Tests;

public class MovingAverageCostCalculatorTests
{
    [Fact]
    public void Incoming_stock_into_empty_inventory_sets_the_cost()
    {
        Assert.Equal(1_500, MovingAverageCostCalculator.Calculate(0, 0, 10_000, 1_500));
    }

    [Fact]
    public void Averages_two_batches()
    {
        // (10.000 x 1.000) + (10.000 x 2.000) = 30.000.000 / 20.000 = 1.500
        Assert.Equal(1_500, MovingAverageCostCalculator.Calculate(10_000, 1_000, 10_000, 2_000));
    }

    [Fact]
    public void Rounds_half_away_from_zero()
    {
        // (3 x 1.001) + (1 x 1.003) = 4.006 / 4 = 1.001,5 -> 1.002
        Assert.Equal(1_002, MovingAverageCostCalculator.Calculate(3, 1_001, 1, 1_003));
    }

    [Fact]
    public void Non_positive_incoming_quantity_keeps_the_current_cost()
    {
        Assert.Equal(1_000, MovingAverageCostCalculator.Calculate(5_000, 1_000, 0, 9_999));
        Assert.Equal(1_000, MovingAverageCostCalculator.Calculate(5_000, 1_000, -100, 9_999));
    }

    [Fact]
    public void Negative_balance_after_incoming_falls_back_to_the_purchase_price()
    {
        Assert.Equal(2_000, MovingAverageCostCalculator.Calculate(-5_000, 1_000, 3_000, 2_000));
    }
}
