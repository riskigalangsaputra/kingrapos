using KingraPOS.Application.Dtos;

namespace KingraPOS.Application.Abstractions.Services;

public interface IProductService
{
    Task<IReadOnlyList<ProductSummaryDto>> SearchAsync(
        string? keyword,
        CancellationToken cancellationToken = default);

    Task<ProductDetailDto> GetDetailAsync(string productId, CancellationToken cancellationToken = default);

    Task<ProductDetailDto> CreateAsync(
        CreateProductRequest request,
        CancellationToken cancellationToken = default);

    Task<ProductDetailDto> UpdateAsync(
        string productId,
        UpdateProductRequest request,
        CancellationToken cancellationToken = default);

    Task SetActiveAsync(string productId, bool isActive, CancellationToken cancellationToken = default);

    Task DeleteAsync(string productId, CancellationToken cancellationToken = default);

    Task<ProductUnitDetailDto> AddUnitAsync(
        string productId,
        ProductUnitRequest request,
        CancellationToken cancellationToken = default);

    Task<ProductUnitDetailDto> UpdateUnitAsync(
        string productUnitId,
        ProductUnitRequest request,
        CancellationToken cancellationToken = default);

    Task RemoveUnitAsync(string productUnitId, CancellationToken cancellationToken = default);

    Task SetUnitPriceAsync(
        string productUnitId,
        long sellingPrice,
        CancellationToken cancellationToken = default);

    Task SetTiersAsync(
        string productUnitId,
        IReadOnlyCollection<ProductPriceTierRequest> tiers,
        CancellationToken cancellationToken = default);

    Task<long> ResolvePriceAsync(string productUnitId, long quantity, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProductPriceHistoryDto>> GetPriceHistoryAsync(
        string productId,
        int take = 50,
        CancellationToken cancellationToken = default);

    /// <summary>Membersihkan penanda "produk baru" yang sudah melewati batas hari pada pengaturan.</summary>
    Task<int> RefreshNewProductBadgesAsync(CancellationToken cancellationToken = default);
}
