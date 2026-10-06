using KingraPOS.Application.Dtos;

namespace KingraPOS.Tests;

internal static class TestData
{
    public const string OwnerUsername = "owner";
    public const string OwnerPassword = "rahasia123";

    public static SetupRequest CreateSetupRequest() => new(
        BusinessName: "Toko Uji",
        OwnerName: "Pemilik Uji",
        PhoneNumber: "08123456789",
        Address: "Jl. Uji No. 1",
        TaxNumber: null,
        ReceiptFooter: "Terima kasih",
        TaxEnabled: true,
        TaxName: "PPN",
        TaxRate: 11.0,
        TaxInclusive: false,
        ServiceFeeEnabled: false,
        CashRounding: 100,
        InvoicePrefix: "INV",
        Timezone: "Asia/Jakarta",
        UtcOffsetMinutes: 420,
        OwnerFullName: "Pemilik Uji",
        OwnerUsername: OwnerUsername,
        OwnerPassword: OwnerPassword);
}
