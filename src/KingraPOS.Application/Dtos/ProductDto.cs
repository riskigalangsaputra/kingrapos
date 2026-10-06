namespace KingraPOS.Application.Dtos;

public record ProductDto(Guid Id, string Sku, string Name, Guid CategoryId, decimal Price, int Stock);
