using KingraPOS.Domain.Pricing;

namespace KingraPOS.Domain.Sales;

public readonly record struct SaleTotals(
    long ItemsAmount,
    long ServiceAmount,
    long TotalGrossAmount,
    long DiscountAmount,
    long PointDiscountAmount,
    long TaxAmount,
    long RoundingAmount,
    long TotalNetAmount);

/// <summary>
/// Rumus total transaksi sesuai PRD:
/// total_gross = items_amount + service_amount
/// total_net   = total_gross - discount - point_discount + (tax bila eksklusif) + rounding
/// </summary>
public static class SaleTotalsCalculator
{
    public static long LineAmount(long unitPrice, long quantity) =>
        (long)Math.Round((decimal)unitPrice * quantity / 1000m, MidpointRounding.AwayFromZero);

    public static SaleTotals Calculate(
        long itemsAmount,
        long serviceAmount,
        long discountAmount,
        long pointDiscountAmount,
        double taxRate,
        bool taxInclusive,
        long cashRounding)
    {
        var gross = itemsAmount + serviceAmount;
        var taxableBase = Math.Max(0, gross - discountAmount - pointDiscountAmount);

        long taxAmount = 0;
        long net;

        if (taxRate > 0)
        {
            if (taxInclusive)
            {
                taxAmount = (long)Math.Round(
                    taxableBase * (decimal)taxRate / (100m + (decimal)taxRate),
                    MidpointRounding.AwayFromZero);

                net = taxableBase;
            }
            else
            {
                taxAmount = (long)Math.Round(
                    taxableBase * (decimal)taxRate / 100m,
                    MidpointRounding.AwayFromZero);

                net = taxableBase + taxAmount;
            }
        }
        else
        {
            net = taxableBase;
        }

        var rounded = CurrencyRounding.Round(net, cashRounding);

        return new SaleTotals(
            itemsAmount,
            serviceAmount,
            gross,
            discountAmount,
            pointDiscountAmount,
            taxAmount,
            rounded - net,
            rounded);
    }
}
