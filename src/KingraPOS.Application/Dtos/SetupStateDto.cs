namespace KingraPOS.Application.Dtos;

public record BusinessProfileDto(
    string Name,
    string OwnerName,
    string? PhoneNumber,
    string? Address,
    string? TaxNumber,
    string? ReceiptFooter);

public record AppSettingsDto(
    bool TaxEnabled,
    string TaxName,
    double TaxRate,
    bool TaxInclusive,
    bool ServiceFeeEnabled,
    bool AllowNegativeStock,
    long CashRounding,
    bool AutoPrintReceipt,
    int NewProductBadgeDays,
    int PriceTrendBadgeDays,
    string InvoicePrefix,
    string Timezone,
    int UtcOffsetMinutes);

public record SetupStateDto(
    bool IsCompleted,
    BusinessProfileDto? BusinessProfile,
    AppSettingsDto Settings);
