using KingraPOS.Application.Stock;
using KingraPOS.Domain.Enums;

namespace KingraPOS.Application.Dtos;

public record InventoryDto(
    string ProductId,
    string Sku,
    string ProductName,
    string BaseUnitName,
    long StockQuantity,
    long RejectQuantity,
    long LowStockThreshold,
    bool IsLowStock,
    bool TrackStock,
    DateTimeOffset? LastMutationAt);

public record StockAdjustmentItemRequest(
    string ProductId,
    string? ProductUnitId,
    long Quantity,
    long? CostPrice);

public record StockAdjustmentRequest(
    StockAdjustmentKind Kind,
    IReadOnlyCollection<StockAdjustmentItemRequest> Items,
    string? SupplierId = null,
    string? SupplierContactId = null,
    string? ReferenceNumber = null,
    string? Notes = null);

public record StockMutationDto(
    string Id,
    string ProductId,
    string ProductName,
    StockMutationType MutationType,
    StockBucket StockBucket,
    long Quantity,
    long? StockBefore,
    long? StockAfter,
    StockReferenceType? ReferenceType,
    string? ReferenceId,
    string? Notes,
    DateTimeOffset CreatedAt,
    string? UserName);

public record RejectRequest(
    string ProductId,
    long Quantity,
    string RejectReason,
    string? SupplierId = null,
    string? Notes = null);

public record RejectLogDto(
    string Id,
    string ProductId,
    string ProductName,
    string RejectReason,
    RejectStatus Status,
    long Quantity,
    long CostPriceAtIncident,
    long TotalLossAmount,
    string? SupplierId,
    DateTimeOffset? ResolvedAt,
    string? ResolvedByUserName,
    string? Notes,
    DateTimeOffset CreatedAt);
