using KingraPOS.Application.Dtos;
using KingraPOS.Domain.Entities;

namespace KingraPOS.Application.Mappings;

public static class DtoMappingExtensions
{
    public static CategoryDto ToDto(this Category category) =>
        new(category.Id, category.Name, category.Description);

    public static ProductDto ToDto(this Product product) =>
        new(product.Id, product.Sku, product.Name, product.CategoryId, product.Price, product.Stock);

    public static SaleItemDto ToDto(this SaleItem item) =>
        new(item.ProductId, item.ProductName, item.Quantity, item.UnitPrice, item.Subtotal);

    public static SaleDto ToDto(this Sale sale) =>
        new(
            sale.Id,
            sale.InvoiceNumber,
            sale.CreatedAt,
            sale.TotalAmount,
            sale.Items.Select(item => item.ToDto()).ToList());
}
