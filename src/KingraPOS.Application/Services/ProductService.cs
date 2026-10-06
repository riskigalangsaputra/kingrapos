using KingraPOS.Application.Abstractions;
using KingraPOS.Application.Dtos;
using KingraPOS.Application.Mappings;
using KingraPOS.Domain.Entities;
using KingraPOS.Domain.Interfaces.Repositories;

namespace KingraPOS.Application.Services;

public class ProductService : IProductService
{
    private readonly IProductRepository _productRepository;
    private readonly ICategoryRepository _categoryRepository;

    public ProductService(IProductRepository productRepository, ICategoryRepository categoryRepository)
    {
        _productRepository = productRepository;
        _categoryRepository = categoryRepository;
    }

    public async Task<IReadOnlyList<ProductDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var products = await _productRepository.GetAllAsync(cancellationToken);
        return products.Select(product => product.ToDto()).ToList();
    }

    public async Task<IReadOnlyList<ProductDto>> SearchAsync(string keyword, CancellationToken cancellationToken = default)
    {
        var products = await _productRepository.SearchAsync(keyword, cancellationToken);
        return products.Select(product => product.ToDto()).ToList();
    }

    public async Task<ProductDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var product = await _productRepository.GetByIdAsync(id, cancellationToken);
        return product?.ToDto();
    }

    public async Task<ProductDto> CreateAsync(
        string sku,
        string name,
        Guid categoryId,
        decimal price,
        int stock,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sku))
            throw new ArgumentException("SKU tidak boleh kosong.", nameof(sku));

        if (price < 0)
            throw new ArgumentOutOfRangeException(nameof(price), "Harga tidak boleh negatif.");

        if (stock < 0)
            throw new ArgumentOutOfRangeException(nameof(stock), "Stok tidak boleh negatif.");

        var existing = await _productRepository.GetBySkuAsync(sku, cancellationToken);
        if (existing is not null)
            throw new InvalidOperationException($"Produk dengan SKU '{sku}' sudah terdaftar.");

        var category = await _categoryRepository.GetByIdAsync(categoryId, cancellationToken)
            ?? throw new InvalidOperationException($"Kategori dengan Id '{categoryId}' tidak ditemukan.");

        var product = new Product
        {
            Sku = sku,
            Name = name,
            CategoryId = category.Id,
            Price = price,
            Stock = stock
        };

        await _productRepository.AddAsync(product, cancellationToken);
        return product.ToDto();
    }

    public async Task UpdateAsync(ProductDto product, CancellationToken cancellationToken = default)
    {
        var existing = await _productRepository.GetByIdAsync(product.Id, cancellationToken)
            ?? throw new InvalidOperationException($"Produk dengan Id '{product.Id}' tidak ditemukan.");

        existing.Sku = product.Sku;
        existing.Name = product.Name;
        existing.CategoryId = product.CategoryId;
        existing.Price = product.Price;
        existing.Stock = product.Stock;

        await _productRepository.UpdateAsync(existing, cancellationToken);
    }

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
        _productRepository.DeleteAsync(id, cancellationToken);
}
