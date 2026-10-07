using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentValidation;
using KingraPOS.Application.Abstractions.Services;
using KingraPOS.Application.Dtos;
using KingraPOS.Application.Security;
using KingraPOS.UI.Services;

namespace KingraPOS.UI.ViewModels.Pages;

public sealed record PaymentMethodOption(string Value, string Name)
{
    public static IReadOnlyList<PaymentMethodOption> All { get; } = new[]
    {
        new PaymentMethodOption("CASH", "Tunai"),
        new PaymentMethodOption("TRANSFER", "Transfer Bank"),
        new PaymentMethodOption("QRIS", "QRIS"),
        new PaymentMethodOption("EWALLET", "E-Wallet"),
        new PaymentMethodOption("CARD", "Kartu"),
        new PaymentMethodOption("OTHER", "Lainnya")
    };
}

public sealed record HeldCart(string Label, IReadOnlyList<CartLineRequest> Lines, long TransactionDiscountAmount);

public partial class CartLineViewModel : ObservableObject
{
    public CartLineViewModel(
        string productId,
        string productName,
        string? productUnitId,
        string unitName,
        long unitPrice,
        long quantity,
        long discountAmount = 0)
    {
        ProductId = productId;
        ProductName = productName;
        ProductUnitId = productUnitId;
        UnitName = unitName;
        UnitPrice = unitPrice;
        Quantity = quantity;
        DiscountAmount = discountAmount;
        Recalculate();
    }

    public string ProductId { get; }

    public string ProductName { get; }

    public string? ProductUnitId { get; }

    public string UnitName { get; }

    [ObservableProperty]
    private long _unitPrice;

    [ObservableProperty]
    private long _quantity;

    [ObservableProperty]
    private long _discountAmount;

    [ObservableProperty]
    private bool _applyServiceFee;

    [ObservableProperty]
    private long? _serviceFee;

    [ObservableProperty]
    private long _subtotal;

    public CartLineRequest ToRequest() =>
        new(ProductId, ProductUnitId, Quantity, null, DiscountAmount, ApplyServiceFee, ServiceFee);

    partial void OnQuantityChanged(long value) => Recalculate();

    partial void OnDiscountAmountChanged(long value) => Recalculate();

    partial void OnUnitPriceChanged(long value) => Recalculate();

    private void Recalculate()
    {
        var amount = (long)Math.Round((decimal)UnitPrice * Quantity / 1000m, MidpointRounding.AwayFromZero);
        Subtotal = Math.Max(0, amount - DiscountAmount);
    }
}

public partial class CashierViewModel : ObservableObject
{
    private readonly ITransactionService _transactionService;
    private readonly IShiftService _shiftService;
    private readonly IProductService _productService;
    private readonly IPermissionService _permissionService;
    private readonly IReceiptPrinterService _receiptPrinter;
    private readonly IValidator<CheckoutRequest> _checkoutValidator;
    private readonly IValidator<VoidTransactionRequest> _voidValidator;

    [ObservableProperty]
    private string? _keyword;

    [ObservableProperty]
    private ProductSummaryDto? _selectedProduct;

    [ObservableProperty]
    private ProductUnitDetailDto? _selectedUnit;

    [ObservableProperty]
    private long _quantity = 1000;

    [ObservableProperty]
    private CartLineViewModel? _selectedLine;

    [ObservableProperty]
    private PaymentMethodOption? _selectedPaymentMethod;

    [ObservableProperty]
    private long _amountPaid;

    [ObservableProperty]
    private long _transactionDiscountAmount;

    [ObservableProperty]
    private string? _paymentProvider;

    [ObservableProperty]
    private string? _paymentReference;

    [ObservableProperty]
    private string? _notes;

    [ObservableProperty]
    private SaleQuoteDto? _quote;

    [ObservableProperty]
    private TransactionDto? _lastTransaction;

    [ObservableProperty]
    private string? _receiptText;

    [ObservableProperty]
    private HeldCart? _selectedHeld;

    [ObservableProperty]
    private TransactionSummaryDto? _selectedTransaction;

    [ObservableProperty]
    private string? _voidReason;

    [ObservableProperty]
    private string? _message;

    [ObservableProperty]
    private bool _isBusy;

    public CashierViewModel(
        ITransactionService transactionService,
        IShiftService shiftService,
        IProductService productService,
        IPermissionService permissionService,
        IReceiptPrinterService receiptPrinter,
        IValidator<CheckoutRequest> checkoutValidator,
        IValidator<VoidTransactionRequest> voidValidator)
    {
        _transactionService = transactionService;
        _shiftService = shiftService;
        _productService = productService;
        _permissionService = permissionService;
        _receiptPrinter = receiptPrinter;
        _checkoutValidator = checkoutValidator;
        _voidValidator = voidValidator;

        _selectedPaymentMethod = PaymentMethodOption.All[0];
    }

    public ObservableCollection<ProductSummaryDto> SearchResults { get; } = new();

    public ObservableCollection<ProductUnitDetailDto> Units { get; } = new();

    public ObservableCollection<CartLineViewModel> Cart { get; } = new();

    public ObservableCollection<TransactionSummaryDto> History { get; } = new();

    public ObservableCollection<HeldCart> HeldCarts { get; } = new();

    public IReadOnlyList<PaymentMethodOption> PaymentMethods => PaymentMethodOption.All;

    public bool CanCreate => _permissionService.HasPermission(PermissionCatalog.SalesCreate);

    public bool CanVoid => _permissionService.HasPermission(PermissionCatalog.SalesVoid);

    public bool CanViewHistory => _permissionService.HasPermission(PermissionCatalog.SalesViewHistory);

    public bool CanDiscount => _permissionService.HasPermission(PermissionCatalog.SalesDiscount);

    public bool HasOpenShift { get; private set; }

    public async Task InitializeAsync()
    {
        await RefreshShiftAsync();
        await SearchAsync();
        await LoadHistoryAsync();
        await RecalculateAsync();
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        IsBusy = true;

        try
        {
            var results = await _productService.SearchAsync(Keyword);

            SearchResults.Clear();
            foreach (var product in results)
                SearchResults.Add(product);
        }
        catch (Exception exception)
        {
            Message = exception.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task AddLineAsync()
    {
        Message = null;

        if (SelectedProduct is null)
        {
            Message = "Pilih produk terlebih dahulu.";
            return;
        }

        if (SelectedUnit is null)
        {
            Message = "Pilih satuan jual terlebih dahulu.";
            return;
        }

        if (Quantity <= 0)
        {
            Message = "Jumlah harus lebih dari nol.";
            return;
        }

        Cart.Add(new CartLineViewModel(
            SelectedProduct.Id,
            SelectedProduct.Name,
            SelectedUnit.Id,
            SelectedUnit.UnitName,
            SelectedUnit.SellingPrice,
            Quantity));

        Quantity = 1000;
        SelectedProduct = null;
        Units.Clear();
        SelectedUnit = null;

        await RecalculateAsync();
    }

    [RelayCommand]
    private async Task RemoveLineAsync()
    {
        if (SelectedLine is null)
            return;

        Cart.Remove(SelectedLine);
        SelectedLine = null;

        await RecalculateAsync();
    }

    [RelayCommand]
    private async Task ClearCartAsync()
    {
        Cart.Clear();
        TransactionDiscountAmount = 0;

        await RecalculateAsync();
    }

    [RelayCommand]
    private async Task RecalculateAsync()
    {
        Message = null;

        if (Cart.Count == 0)
        {
            Quote = null;
            AmountPaid = 0;
            return;
        }

        try
        {
            Quote = await _transactionService.QuoteAsync(BuildRequest());
            AmountPaid = Quote.TotalNetAmount;
        }
        catch (Exception exception)
        {
            Quote = null;
            Message = exception.Message;
        }
    }

    [RelayCommand]
    private async Task CheckoutAsync()
    {
        Message = null;

        if (!CanCreate)
        {
            Message = "Anda tidak memiliki izin membuat transaksi.";
            return;
        }

        if (!HasOpenShift)
        {
            Message = "Belum ada shift terbuka. Buka shift terlebih dahulu.";
            return;
        }

        if (Cart.Count == 0)
        {
            Message = "Keranjang masih kosong.";
            return;
        }

        var request = BuildRequest();

        var validation = await _checkoutValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            Message = string.Join(Environment.NewLine, validation.Errors.Select(error => error.ErrorMessage));
            return;
        }

        IsBusy = true;

        try
        {
            var transaction = await _transactionService.CheckoutAsync(request);

            LastTransaction = transaction;
            ReceiptText = await _receiptPrinter.FormatAsync(transaction);
            Message = $"Transaksi {transaction.InvoiceNumber} tersimpan.";

            Cart.Clear();
            TransactionDiscountAmount = 0;
            AmountPaid = 0;
            Quote = null;

            var printed = await _receiptPrinter.TryAutoPrintAsync(transaction);
            if (!printed && CanViewHistory)
                Message += " Struk tidak tercetak otomatis; cetak manual bila perlu.";

            await RefreshShiftAsync();
            await LoadHistoryAsync();
        }
        catch (Exception exception)
        {
            Message = exception.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task PrintReceiptAsync()
    {
        if (LastTransaction is null)
        {
            Message = "Belum ada transaksi untuk dicetak.";
            return;
        }

        try
        {
            await _receiptPrinter.PrintAsync(LastTransaction);
            Message = "Struk dicetak.";
        }
        catch (Exception exception)
        {
            Message = $"Cetak struk gagal: {exception.Message}";
        }
    }

    [RelayCommand]
    private void HoldCart()
    {
        if (Cart.Count == 0)
        {
            Message = "Keranjang masih kosong.";
            return;
        }

        var label = $"Tahan {DateTime.Now:HH:mm:ss} ({Cart.Count} item)";
        HeldCarts.Add(new HeldCart(
            label,
            Cart.Select(line => line.ToRequest()).ToList(),
            TransactionDiscountAmount));

        Cart.Clear();
        TransactionDiscountAmount = 0;
        Quote = null;
        Message = $"Transaksi '{label}' ditahan.";
    }

    [RelayCommand]
    private async Task ResumeCartAsync()
    {
        if (SelectedHeld is null)
        {
            Message = "Pilih transaksi tertahan terlebih dahulu.";
            return;
        }

        Cart.Clear();

        foreach (var line in SelectedHeld.Lines)
        {
            var product = await _productService.GetDetailAsync(line.ProductId);
            var unit = product.Units.FirstOrDefault(row => row.Id == line.ProductUnitId)
                ?? product.Units.FirstOrDefault(row => row.IsBaseUnit);

            Cart.Add(new CartLineViewModel(
                product.Id,
                product.Name,
                unit?.Id,
                unit?.UnitName ?? product.BaseUnitName,
                unit?.SellingPrice ?? 0,
                line.Quantity,
                line.DiscountAmount));
        }

        TransactionDiscountAmount = SelectedHeld.TransactionDiscountAmount;
        HeldCarts.Remove(SelectedHeld);
        SelectedHeld = null;

        await RecalculateAsync();
    }

    [RelayCommand]
    private async Task VoidAsync()
    {
        Message = null;

        if (!CanVoid)
        {
            Message = "Anda tidak memiliki izin membatalkan transaksi.";
            return;
        }

        if (SelectedTransaction is null)
        {
            Message = "Pilih transaksi yang akan dibatalkan.";
            return;
        }

        var request = new VoidTransactionRequest(SelectedTransaction.Id, VoidReason ?? string.Empty);

        var validation = await _voidValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            Message = string.Join(Environment.NewLine, validation.Errors.Select(error => error.ErrorMessage));
            return;
        }

        IsBusy = true;

        try
        {
            await _transactionService.VoidAsync(request);
            Message = $"Transaksi {SelectedTransaction.InvoiceNumber} dibatalkan.";
            VoidReason = null;
            SelectedTransaction = null;

            await LoadHistoryAsync();
        }
        catch (Exception exception)
        {
            Message = exception.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    partial void OnSelectedProductChanged(ProductSummaryDto? value)
    {
        Units.Clear();
        SelectedUnit = null;

        if (value is not null)
            _ = LoadUnitsAsync(value.Id);
    }

    private async Task LoadUnitsAsync(string productId)
    {
        try
        {
            var detail = await _productService.GetDetailAsync(productId);

            Units.Clear();
            foreach (var unit in detail.Units)
                Units.Add(unit);

            SelectedUnit = detail.Units.FirstOrDefault(unit => unit.IsBaseUnit) ?? detail.Units.FirstOrDefault();
        }
        catch (Exception exception)
        {
            Message = exception.Message;
        }
    }

    private CheckoutRequest BuildRequest() => new(
        Cart.Select(line => line.ToRequest()).ToList(),
        SelectedPaymentMethod?.Value ?? "CASH",
        PaymentProvider,
        PaymentReference,
        AmountPaid,
        TransactionDiscountAmount,
        null,
        Notes);

    private async Task RefreshShiftAsync()
    {
        try
        {
            var shift = await _shiftService.GetOpenShiftAsync();
            HasOpenShift = shift is not null;
        }
        catch
        {
            HasOpenShift = false;
        }
    }

    private async Task LoadHistoryAsync()
    {
        if (!CanViewHistory)
            return;

        try
        {
            var transactions = await _transactionService.GetRecentAsync();

            History.Clear();
            foreach (var transaction in transactions)
                History.Add(transaction);
        }
        catch (Exception exception)
        {
            Message = exception.Message;
        }
    }
}
