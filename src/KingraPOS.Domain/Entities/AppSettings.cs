using KingraPOS.Domain.Common;

namespace KingraPOS.Domain.Entities;

public class AppSettings : Entity
{
    public bool TaxEnabled { get; set; }

    public string TaxName { get; set; } = "PPN";

    public double TaxRate { get; set; } = 11.0;

    public bool TaxInclusive { get; set; }

    public bool ServiceFeeEnabled { get; set; }

    public bool AllowNegativeStock { get; set; }

    public long CashRounding { get; set; }

    public bool AutoPrintReceipt { get; set; } = true;

    public int NewProductBadgeDays { get; set; } = 7;

    public int PriceTrendBadgeDays { get; set; } = 7;

    public string InvoicePrefix { get; set; } = "INV";

    public string Timezone { get; set; } = "Asia/Jakarta";

    public int UtcOffsetMinutes { get; set; } = 420;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
