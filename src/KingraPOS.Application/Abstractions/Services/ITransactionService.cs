using KingraPOS.Application.Dtos;

namespace KingraPOS.Application.Abstractions.Services;

public interface ITransactionService
{
    Task<SaleQuoteDto> QuoteAsync(CheckoutRequest request, CancellationToken cancellationToken = default);

    Task<TransactionDto> CheckoutAsync(CheckoutRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TransactionSummaryDto>> GetRecentAsync(
        int take = 50,
        CancellationToken cancellationToken = default);

    Task<TransactionDto> GetDetailAsync(string transactionId, CancellationToken cancellationToken = default);

    Task VoidAsync(VoidTransactionRequest request, CancellationToken cancellationToken = default);
}
