using KingraPOS.Domain.Common;
using KingraPOS.Domain.Enums;

namespace KingraPOS.Domain.Entities;

public class Sale : Entity
{
    private readonly List<SaleItem> _items = new();

    public string InvoiceNumber { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public PaymentMethod PaymentMethod { get; set; }

    public SaleStatus Status { get; set; } = SaleStatus.Completed;

    public IReadOnlyCollection<SaleItem> Items => _items;

    public decimal TotalAmount => _items.Sum(item => item.Subtotal);

    public void AddItem(SaleItem item) => _items.Add(item);
}
