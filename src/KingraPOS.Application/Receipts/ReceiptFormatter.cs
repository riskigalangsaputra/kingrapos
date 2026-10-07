using System.Globalization;
using System.Text;
using KingraPOS.Application.Dtos;
using KingraPOS.Domain.Enums;

namespace KingraPOS.Application.Receipts;

public static class ReceiptFormatter
{
    private static readonly CultureInfo Currency = CultureInfo.GetCultureInfo("id-ID");

    public static int ColumnsFor(int paperWidthMm) => paperWidthMm >= 80 ? 48 : 32;

    public static string Format(TransactionDto transaction, BusinessProfileDto? profile, int paperWidthMm)
    {
        var columns = ColumnsFor(paperWidthMm);
        var builder = new StringBuilder();

        void AppendLine(string text = "") => builder.AppendLine(Truncate(text, columns));

        void AppendPair(string left, string right)
        {
            var space = columns - left.Length - right.Length;

            builder.AppendLine(space > 0
                ? left + new string(' ', space) + right
                : left + " " + right);
        }

        void Separator() => builder.AppendLine(new string('-', columns));

        AppendLine(Center(profile?.Name ?? "Kingra POS", columns));

        if (!string.IsNullOrWhiteSpace(profile?.Address))
            AppendLine(Center(profile.Address!, columns));

        if (!string.IsNullOrWhiteSpace(profile?.PhoneNumber))
            AppendLine(Center(profile.PhoneNumber!, columns));

        if (!string.IsNullOrWhiteSpace(profile?.TaxNumber))
            AppendLine(Center($"NPWP: {profile.TaxNumber}", columns));

        Separator();
        AppendPair("Invoice", transaction.InvoiceNumber);
        AppendPair("Waktu", transaction.CreatedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm"));
        AppendPair("Kasir", transaction.UserName);

        if (!string.IsNullOrWhiteSpace(transaction.CustomerName))
            AppendPair("Pelanggan", transaction.CustomerName!);

        Separator();

        foreach (var item in transaction.Items)
        {
            var name = item.ItemType == TransactionItemType.SERVICE
                ? item.Description ?? "Jasa"
                : item.ProductName ?? item.Description ?? "-";

            AppendLine(name);

            var quantity = item.Quantity / 1000m;
            var unit = string.IsNullOrWhiteSpace(item.UnitName) ? string.Empty : $" {item.UnitName}";
            var unitPrice = Money(item.SellingPrice);

            AppendPair($"  {quantity.ToString("0.###", Currency)}{unit} x {unitPrice}", Money(item.SubtotalPrice));
        }

        Separator();
        AppendPair("Total barang", Money(transaction.ItemsAmount));

        if (transaction.ServiceAmount > 0)
            AppendPair("Total jasa", Money(transaction.ServiceAmount));

        if (transaction.DiscountAmount > 0)
            AppendPair("Diskon", "-" + Money(transaction.DiscountAmount));

        if (transaction.PointDiscountAmount > 0)
            AppendPair("Diskon poin", "-" + Money(transaction.PointDiscountAmount));

        if (transaction.TaxAmount > 0 && transaction.TaxRate > 0)
        {
            var suffix = transaction.TaxInclusive ? " (incl.)" : string.Empty;

            AppendPair($"{transaction.TaxName} {transaction.TaxRate:0.##}%{suffix}", Money(transaction.TaxAmount));
        }

        if (transaction.RoundingAmount != 0)
            AppendPair("Pembulatan", Money(transaction.RoundingAmount));

        Separator();
        AppendPair("TOTAL", Money(transaction.TotalNetAmount));
        Separator();
        AppendPair("Bayar (" + transaction.PaymentMethod + ")", Money(transaction.AmountPaid));
        AppendPair("Kembali", Money(transaction.AmountChange));

        if (transaction.PointsEarned > 0)
            AppendPair("Poin didapat", transaction.PointsEarned.ToString(Currency));

        if (transaction.PointsUsed > 0)
            AppendPair("Poin dipakai", transaction.PointsUsed.ToString(Currency));

        if (transaction.Status == TransactionStatus.VOIDED)
        {
            Separator();
            AppendLine(Center("*** VOID ***", columns));

            if (!string.IsNullOrWhiteSpace(transaction.VoidReason))
                AppendLine($"Alasan: {transaction.VoidReason}");
        }

        Separator();

        if (!string.IsNullOrWhiteSpace(profile?.ReceiptFooter))
        {
            foreach (var line in Wrap(profile!.ReceiptFooter!, columns))
                AppendLine(Center(line, columns));
        }

        return builder.ToString();
    }

    private static string Money(long amount) =>
        "Rp " + amount.ToString("N0", Currency);

    private static string Center(string text, int columns)
    {
        if (text.Length >= columns)
            return text;

        var padding = (columns - text.Length) / 2;

        return new string(' ', padding) + text;
    }

    private static string Truncate(string text, int columns) =>
        text.Length <= columns ? text : text[..columns];

    private static IEnumerable<string> Wrap(string text, int columns)
    {
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var line = new StringBuilder();

        foreach (var word in words)
        {
            if (line.Length > 0 && line.Length + 1 + word.Length > columns)
            {
                yield return line.ToString();
                line.Clear();
            }

            if (line.Length > 0)
                line.Append(' ');

            line.Append(word);
        }

        if (line.Length > 0)
            yield return line.ToString();
    }
}
