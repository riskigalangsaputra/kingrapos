namespace KingraPOS.Application.Dtos;

public record CheckoutItemRequest(Guid ProductId, int Quantity);
