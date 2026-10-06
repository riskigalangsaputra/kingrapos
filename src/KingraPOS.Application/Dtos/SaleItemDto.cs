namespace KingraPOS.Application.Dtos;

public record SaleItemDto(Guid ProductId, string ProductName, int Quantity, decimal UnitPrice, decimal Subtotal);
