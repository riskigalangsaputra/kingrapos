using KingraPOS.Application.Dtos;
using KingraPOS.Application.Stock;

namespace KingraPOS.Application.Abstractions.Services;

public interface IStockService
{
    Task<IReadOnlyList<InventoryDto>> GetInventoriesAsync(
        string? keyword = null,
        bool lowStockOnly = false,
        CancellationToken cancellationToken = default);

    Task<InventoryDto> GetInventoryAsync(string productId, CancellationToken cancellationToken = default);

    Task SetLowStockThresholdAsync(
        string productId,
        long threshold,
        CancellationToken cancellationToken = default);

    Task<string> RecordAdjustmentAsync(
        StockAdjustmentRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StockMutationDto>> GetMutationsAsync(
        string? productId = null,
        int take = 100,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RejectLogDto>> GetRejectLogsAsync(
        bool openOnly = false,
        CancellationToken cancellationToken = default);

    Task<string> RecordRejectAsync(RejectRequest request, CancellationToken cancellationToken = default);

    Task ResolveRejectAsync(
        string rejectLogId,
        RejectResolution resolution,
        string? supplierId = null,
        CancellationToken cancellationToken = default);
}
