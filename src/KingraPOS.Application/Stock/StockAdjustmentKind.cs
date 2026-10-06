using KingraPOS.Domain.Enums;

namespace KingraPOS.Application.Stock;

public enum StockAdjustmentKind
{
    Opening = 0,
    StockIn = 1,
    InputError = 2,
    StockCount = 3,
    Lost = 4,
    Other = 5
}

public static class StockAdjustmentRules
{
    /// <summary>
    /// Pemetaan ke kolom skema. "Stok Awal" tidak punya nilai sendiri di CHECK
    /// <c>stock_adjustments.reason</c>, sehingga dipetakan ke OTHER sementara mutasinya bertipe OPENING.
    /// </summary>
    public static (StockAdjustmentReason Reason, StockMutationType MutationType) Map(StockAdjustmentKind kind) => kind switch
    {
        StockAdjustmentKind.StockIn => (StockAdjustmentReason.STOCK_IN, StockMutationType.STOCK_IN),
        StockAdjustmentKind.Opening => (StockAdjustmentReason.OTHER, StockMutationType.OPENING),
        StockAdjustmentKind.InputError => (StockAdjustmentReason.INPUT_ERROR, StockMutationType.ADJUSTMENT),
        StockAdjustmentKind.StockCount => (StockAdjustmentReason.STOCK_COUNT, StockMutationType.ADJUSTMENT),
        StockAdjustmentKind.Lost => (StockAdjustmentReason.LOST, StockMutationType.ADJUSTMENT),
        _ => (StockAdjustmentReason.OTHER, StockMutationType.ADJUSTMENT)
    };

    /// <summary>True bila input user adalah saldo akhir; false bila input adalah jumlah tambahan.</summary>
    public static bool IsAbsoluteQuantity(StockAdjustmentKind kind) =>
        kind is not (StockAdjustmentKind.StockIn or StockAdjustmentKind.Opening);

    public static bool UpdatesAverageCost(StockAdjustmentKind kind) => kind == StockAdjustmentKind.StockIn;
}
