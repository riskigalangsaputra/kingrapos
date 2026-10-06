using KingraPOS.Application.Abstractions;
using KingraPOS.Application.Dtos;
using KingraPOS.Application.Mappings;
using KingraPOS.Domain.Entities;
using KingraPOS.Domain.Enums;
using KingraPOS.Domain.Interfaces.Repositories;

namespace KingraPOS.Application.Services;

public class SaleService : ISaleService
{
    private readonly ISaleRepository _saleRepository;
    private readonly IProductRepository _productRepository;

    public SaleService(ISaleRepository saleRepository, IProductRepository productRepository)
    {
        _saleRepository = saleRepository;
        _productRepository = productRepository;
    }

    public async Task<SaleDto> CheckoutAsync(CheckoutRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Items.Count == 0)
            throw new InvalidOperationException("Transaksi tidak boleh kosong.");

        var sale = new Sale
        {
            InvoiceNumber = GenerateInvoiceNumber(),
            CreatedAt = DateTime.Now,
            PaymentMethod = request.PaymentMethod,
            Status = SaleStatus.Completed
        };

        foreach (var requestItem in request.Items)
        {
            if (requestItem.Quantity <= 0)
                throw new InvalidOperationException("Jumlah produk harus lebih dari nol.");

            var product = await _productRepository.GetByIdAsync(requestItem.ProductId, cancellationToken)
                ?? throw new InvalidOperationException($"Produk dengan Id '{requestItem.ProductId}' tidak ditemukan.");

            product.ReduceStock(requestItem.Quantity);

            sale.AddItem(new SaleItem
            {
                ProductId = product.Id,
                ProductName = product.Name,
                Quantity = requestItem.Quantity,
                UnitPrice = product.Price
            });

            await _productRepository.UpdateAsync(product, cancellationToken);
        }

        await _saleRepository.AddAsync(sale, cancellationToken);
        return sale.ToDto();
    }

    public async Task<IReadOnlyList<SaleDto>> GetTodaySalesAsync(CancellationToken cancellationToken = default)
    {
        var sales = await _saleRepository.GetByDateAsync(DateTime.Today, cancellationToken);
        return sales.Select(sale => sale.ToDto()).ToList();
    }

    public async Task<decimal> GetTodayRevenueAsync(CancellationToken cancellationToken = default)
    {
        var sales = await _saleRepository.GetByDateAsync(DateTime.Today, cancellationToken);
        return sales.Sum(sale => sale.TotalAmount);
    }

    private static string GenerateInvoiceNumber() => $"INV-{DateTime.Now:yyyyMMdd-HHmmss}";
}
