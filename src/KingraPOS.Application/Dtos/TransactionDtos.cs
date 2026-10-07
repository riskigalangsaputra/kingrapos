using KingraPOS.Domain.Enums;

namespace KingraPOS.Application.Dtos;

public record CartLineRequest(
    string ProductId,
    string? ProductUnitId,
    long Quantity,
    long? SellingPriceOverride = null,
    long DiscountAmount = 0,
    bool ApplyServiceFee = false,
    long? ServiceFee = null);

public record CheckoutRequest(
    IReadOnlyCollection<CartLineRequest> Lines,
    string PaymentMethod,
    string? PaymentProvider = null,
    string? PaymentReference = null,
    long AmountPaid = 0,
    long TransactionDiscountAmount = 0,
    string? CustomerId = null,
    string? Notes = null);

public record VoidTransactionRequest(string TransactionId, string Reason);

public record SaleLineDto(
    string ProductId,
    string ProductName,
    string? ProductUnitId,
    string UnitName,
    long Quantity,
    long ConversionFactor,
    long QuantityBase,
    long SellingPrice,
    long? CostPrice,
    long DiscountAmount,
    long SubtotalPrice,
    bool IsService,
    long? ServiceFee);

public record SaleQuoteDto(
    IReadOnlyList<SaleLineDto> Lines,
    long ItemsAmount,
    long ServiceAmount,
    long TotalGrossAmount,
    long DiscountAmount,
    long PointDiscountAmount,
    double TaxRate,
    string TaxName,
    bool TaxInclusive,
    long TaxAmount,
    long RoundingAmount,
    long TotalNetAmount,
    bool CanApplyDiscount,
    bool CanEditServiceFee);

public record TransactionItemDto(
    string Id,
    TransactionItemType ItemType,
    string? ProductId,
    string? ProductName,
    string? UnitName,
    string? Description,
    long Quantity,
    long ConversionFactor,
    long QuantityBase,
    long? CostPrice,
    long SellingPrice,
    long DiscountAmount,
    long SubtotalPrice,
    int SortOrder);

public record TransactionDto(
    string Id,
    string InvoiceNumber,
    string UserName,
    string? CustomerName,
    string ShiftId,
    DateTimeOffset CreatedAt,
    long ItemsAmount,
    long ServiceAmount,
    long TotalGrossAmount,
    long DiscountAmount,
    long PointDiscountAmount,
    double TaxRate,
    string TaxName,
    bool TaxInclusive,
    long TaxAmount,
    long RoundingAmount,
    long TotalNetAmount,
    long? TotalCostPrice,
    string PaymentMethod,
    string? PaymentProvider,
    string? PaymentReference,
    long AmountPaid,
    long AmountChange,
    long PointsEarned,
    long PointsUsed,
    TransactionStatus Status,
    DateTimeOffset? VoidedAt,
    string? VoidedByUserName,
    string? VoidReason,
    string? Notes,
    IReadOnlyList<TransactionItemDto> Items);

public record TransactionSummaryDto(
    string Id,
    string InvoiceNumber,
    DateTimeOffset CreatedAt,
    string UserName,
    string? CustomerName,
    long TotalNetAmount,
    string PaymentMethod,
    TransactionStatus Status);
