namespace KingraPOS.Application.Sales;

public static class PaymentMethods
{
    public const string Cash = "CASH";
    public const string Transfer = "TRANSFER";
    public const string Qris = "QRIS";
    public const string EWallet = "EWALLET";
    public const string Card = "CARD";
    public const string Other = "OTHER";

    public static IReadOnlyList<string> All { get; } = new[]
    {
        Cash, Transfer, Qris, EWallet, Card, Other
    };

    public static bool IsKnown(string method) =>
        All.Any(known => string.Equals(known, method, StringComparison.OrdinalIgnoreCase));

    public static bool IsCash(string method) =>
        string.Equals(Cash, method, StringComparison.OrdinalIgnoreCase);

    public static string Normalize(string method) => method.Trim().ToUpperInvariant();
}
