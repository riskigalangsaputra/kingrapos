using KingraPOS.Domain.Common;
using KingraPOS.Domain.Enums;

namespace KingraPOS.Domain.Entities;

public class TransactionItem : Entity
{
    public string TransactionId { get; set; } = string.Empty;

    public string? ParentItemId { get; set; }

    public TransactionItemType ItemType { get; set; } = TransactionItemType.PRODUCT;

    public string? ProductId { get; set; }

    public string? ProductUnitId { get; set; }

    public string? Description { get; set; }

    public string? PriceTierId { get; set; }

    public long Quantity { get; set; }

    public long ConversionFactor { get; set; } = 1000;

    public long QuantityBase { get; set; }

    public long CostPrice { get; set; }

    public long SellingPrice { get; set; }

    public long DiscountAmount { get; set; }

    public long SubtotalPrice { get; set; }

    public int SortOrder { get; set; }
}
