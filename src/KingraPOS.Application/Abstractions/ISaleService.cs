using KingraPOS.Application.Dtos;

namespace KingraPOS.Application.Abstractions;

public interface ISaleService
{
    Task<SaleDto> CheckoutAsync(CheckoutRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SaleDto>> GetTodaySalesAsync(CancellationToken cancellationToken = default);

    Task<decimal> GetTodayRevenueAsync(CancellationToken cancellationToken = default);
}
