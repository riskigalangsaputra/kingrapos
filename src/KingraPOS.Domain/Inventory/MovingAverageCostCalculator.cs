namespace KingraPOS.Domain.Inventory;

public static class MovingAverageCostCalculator
{
    /// <summary>
    /// HPP rata-rata bergerak setelah barang masuk:
    /// ((stok lama x HPP lama) + (qty masuk x harga beli)) / (stok lama + qty masuk).
    /// </summary>
    public static long Calculate(long currentQuantity, long currentCost, long incomingQuantity, long incomingCost)
    {
        if (incomingQuantity <= 0)
            return currentCost;

        var totalQuantity = currentQuantity + incomingQuantity;
        if (totalQuantity <= 0)
            return incomingCost;

        var numerator = ((decimal)currentQuantity * currentCost) + ((decimal)incomingQuantity * incomingCost);

        return (long)Math.Round(numerator / totalQuantity, MidpointRounding.AwayFromZero);
    }
}
