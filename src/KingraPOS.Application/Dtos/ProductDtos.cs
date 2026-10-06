using KingraPOS.Domain.Enums;

namespace KingraPOS.Application.Dtos;

public record ProductSummaryDto(
    string Id,
    string Sku,
    string Name,
    string? CategoryName,
    string BaseUnitName,
    long? BaseCostPrice,
    bool TrackStock,
    bool IsActive,
    bool IsNewProduct);

public record ProductPriceTierDto(string Id, string TierName, long MinimumQuantity, long TierPrice);

public record ProductUnitDetailDto(
    string Id,
    string UnitId,
    string UnitName,
    string? Barcode,
    long ConversionFactor,
    bool IsBaseUnit,
    long SellingPrice,
    PriceTrend PriceTrend,
    long? PreviousSellingPrice,
    DateTimeOffset? PriceChangedAt,
    bool ShowTrendBadge,
    IReadOnlyList<ProductPriceTierDto> Tiers);

public record ProductDetailDto(
    string Id,
    string Sku,
    string Name,
    string? Description,
    string? ImagePath,
    string? CategoryId,
    string? CategoryName,
    string BaseUnitId,
    string BaseUnitName,
    long? BaseCostPrice,
    long? DefaultServiceFee,
    bool TrackStock,
    bool IsNewProduct,
    bool IsActive,
    IReadOnlyList<ProductUnitDetailDto> Units);

public record CreateProductRequest(
    string Sku,
    string Name,
    string? CategoryId,
    string BaseUnitId,
    string? Description,
    long? BaseCostPrice,
    long? DefaultServiceFee,
    bool TrackStock);

public record UpdateProductRequest(
    string Sku,
    string Name,
    string? CategoryId,
    string? Description,
    long? BaseCostPrice,
    long? DefaultServiceFee,
    bool TrackStock,
    bool IsActive);

public record ProductUnitRequest(string UnitId, string? Barcode, long ConversionFactor);

public record ProductPriceTierRequest(string TierName, long MinimumQuantity, long TierPrice);

public record ProductPriceHistoryDto(
    string Id,
    PriceChangeType PriceType,
    long OldPrice,
    long NewPrice,
    string? Notes,
    DateTimeOffset CreatedAt,
    string? UserName);
