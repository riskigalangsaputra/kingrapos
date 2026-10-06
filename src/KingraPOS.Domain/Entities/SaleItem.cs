using KingraPOS.Domain.Common;

namespace KingraPOS.Domain.Entities;

public class SaleItem : Entity
{
    public Guid ProductId { get; set; }

    public string ProductName { get; set; } = string.Empty;

    public int Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal Subtotal => Quantity * UnitPrice;
}
