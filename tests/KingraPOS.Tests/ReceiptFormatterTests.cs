using KingraPOS.Application.Dtos;
using KingraPOS.Application.Receipts;
using KingraPOS.Domain.Enums;

namespace KingraPOS.Tests;

public class ReceiptFormatterTests
{
    private static BusinessProfileDto Profile() => new(
        "Toko Uji",
        "Pemilik Uji",
        "08123456789",
        "Jl. Uji No. 1",
        "12.345.678.9-000.000",
        "Terima kasih telah berbelanja");

    private static TransactionDto Transaction(TransactionStatus status = TransactionStatus.COMPLETED) => new(
        "trx-1",
        "INV-20261005-0001",
        "Pemilik Uji",
        null,
        "shift-1",
        new DateTimeOffset(2026, 10, 5, 3, 30, 0, TimeSpan.Zero),
        10_000,
        0,
        10_000,
        0,
        0,
        11.0,
        "PPN",
        false,
        1_100,
        0,
        11_100,
        700,
        "CASH",
        null,
        null,
        20_000,
        8_900,
        0,
        0,
        status,
        status == TransactionStatus.VOIDED ? DateTimeOffset.UtcNow : null,
        status == TransactionStatus.VOIDED ? "Pemilik Uji" : null,
        status == TransactionStatus.VOIDED ? "Salah input" : null,
        null,
        new[]
        {
            new TransactionItemDto(
                "item-1",
                TransactionItemType.PRODUCT,
                "p1",
                "Keripik",
                "Pcs",
                "Keripik",
                2_000,
                1_000,
                2_000,
                700,
                5_000,
                0,
                10_000,
                0)
        });

    [Fact]
    public void Receipt_contains_the_header_items_and_totals()
    {
        var text = ReceiptFormatter.Format(Transaction(), Profile(), 80);

        Assert.Contains("Toko Uji", text);
        Assert.Contains("INV-20261005-0001", text);
        Assert.Contains("Keripik", text);
        Assert.Contains("Rp 11.100", text);
        Assert.Contains("Terima kasih", text);
    }

    [Fact]
    public void Voided_receipt_is_marked_and_shows_the_reason()
    {
        var text = ReceiptFormatter.Format(Transaction(TransactionStatus.VOIDED), Profile(), 58);

        Assert.Contains("VOID", text);
        Assert.Contains("Salah input", text);
    }

    [Fact]
    public void Paper_width_controls_the_column_count()
    {
        Assert.Equal(48, ReceiptFormatter.ColumnsFor(80));
        Assert.Equal(32, ReceiptFormatter.ColumnsFor(58));
    }
}
