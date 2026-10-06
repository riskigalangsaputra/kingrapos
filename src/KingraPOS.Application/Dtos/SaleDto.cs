namespace KingraPOS.Application.Dtos;

public record SaleDto(
    Guid Id,
    string InvoiceNumber,
    DateTime CreatedAt,
    decimal TotalAmount,
    IReadOnlyCollection<SaleItemDto> Items);
