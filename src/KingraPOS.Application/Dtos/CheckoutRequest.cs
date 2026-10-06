using KingraPOS.Domain.Enums;

namespace KingraPOS.Application.Dtos;

public record CheckoutRequest(IReadOnlyCollection<CheckoutItemRequest> Items, PaymentMethod PaymentMethod);
