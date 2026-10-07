using KingraPOS.Domain.Sales;

namespace KingraPOS.Tests;

public class SaleTotalsCalculatorTests
{
    [Fact]
    public void Line_amount_multiplies_unit_price_by_quantity_over_a_thousand()
    {
        Assert.Equal(10_000, SaleTotalsCalculator.LineAmount(5_000, 2_000));
        Assert.Equal(2_500, SaleTotalsCalculator.LineAmount(5_000, 500));
    }

    [Fact]
    public void Exclusive_tax_is_added_on_top_of_the_net_amount()
    {
        var totals = SaleTotalsCalculator.Calculate(
            itemsAmount: 10_000,
            serviceAmount: 0,
            discountAmount: 0,
            pointDiscountAmount: 0,
            taxRate: 11.0,
            taxInclusive: false,
            cashRounding: 100);

        Assert.Equal(1_100, totals.TaxAmount);
        Assert.Equal(0, totals.RoundingAmount);
        Assert.Equal(11_100, totals.TotalNetAmount);
    }

    [Fact]
    public void Inclusive_tax_is_extracted_from_the_net_amount()
    {
        var totals = SaleTotalsCalculator.Calculate(
            itemsAmount: 11_100,
            serviceAmount: 0,
            discountAmount: 0,
            pointDiscountAmount: 0,
            taxRate: 11.0,
            taxInclusive: true,
            cashRounding: 100);

        Assert.Equal(1_100, totals.TaxAmount);
        Assert.Equal(11_100, totals.TotalNetAmount);
    }

    [Fact]
    public void Rounding_snaps_the_net_amount_to_the_configured_step()
    {
        var totals = SaleTotalsCalculator.Calculate(
            itemsAmount: 10_005,
            serviceAmount: 0,
            discountAmount: 0,
            pointDiscountAmount: 0,
            taxRate: 0,
            taxInclusive: false,
            cashRounding: 100);

        Assert.Equal(-5, totals.RoundingAmount);
        Assert.Equal(10_000, totals.TotalNetAmount);
    }

    [Fact]
    public void Discount_reduces_the_taxable_base()
    {
        var totals = SaleTotalsCalculator.Calculate(
            itemsAmount: 10_000,
            serviceAmount: 0,
            discountAmount: 1_000,
            pointDiscountAmount: 0,
            taxRate: 11.0,
            taxInclusive: false,
            cashRounding: 100);

        Assert.Equal(10_000, totals.TotalGrossAmount);
        Assert.Equal(990, totals.TaxAmount);
        Assert.Equal(10, totals.RoundingAmount);
        Assert.Equal(10_000, totals.TotalNetAmount);
    }

    [Fact]
    public void Service_amount_is_included_in_the_gross_total()
    {
        var totals = SaleTotalsCalculator.Calculate(
            itemsAmount: 10_000,
            serviceAmount: 2_500,
            discountAmount: 0,
            pointDiscountAmount: 0,
            taxRate: 0,
            taxInclusive: false,
            cashRounding: 0);

        Assert.Equal(12_500, totals.TotalGrossAmount);
        Assert.Equal(12_500, totals.TotalNetAmount);
    }
}
