using KingraPOS.Application.Dtos;

namespace KingraPOS.UI.Services;

public interface IReceiptPrinterService
{
    Task<string> FormatAsync(TransactionDto transaction, CancellationToken cancellationToken = default);

    Task PrintAsync(TransactionDto transaction, CancellationToken cancellationToken = default);

    Task<bool> TryAutoPrintAsync(TransactionDto transaction, CancellationToken cancellationToken = default);
}
