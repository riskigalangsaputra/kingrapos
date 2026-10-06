namespace KingraPOS.Application.Dtos;

public record SetupRequest(
    string BusinessName,
    string OwnerName,
    string? PhoneNumber,
    string? Address,
    string? TaxNumber,
    string? ReceiptFooter,
    bool TaxEnabled,
    string TaxName,
    double TaxRate,
    bool TaxInclusive,
    bool ServiceFeeEnabled,
    long CashRounding,
    string InvoicePrefix,
    string Timezone,
    int UtcOffsetMinutes,
    string OwnerFullName,
    string OwnerUsername,
    string OwnerPassword);
