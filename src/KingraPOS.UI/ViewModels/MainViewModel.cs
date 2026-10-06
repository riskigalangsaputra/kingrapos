using System.Collections.ObjectModel;
using KingraPOS.Application.Abstractions;
using KingraPOS.Application.Dtos;
using KingraPOS.Domain.Enums;
using KingraPOS.UI.Common;

namespace KingraPOS.UI.ViewModels;

public class MainViewModel : ObservableObject
{
    private readonly IProductService _productService;
    private readonly ISaleService _saleService;

    private string _searchKeyword = string.Empty;
    private ProductDto? _selectedProduct;
    private string _statusMessage = "Siap.";
    private decimal _todayRevenue;
    private int _todayTransactions;
    private bool _isBusy;

    public MainViewModel(IProductService productService, ISaleService saleService)
    {
        _productService = productService;
        _saleService = saleService;

        Products = new ObservableCollection<ProductDto>();
        SearchCommand = new RelayCommand(async _ => await LoadProductsAsync(), _ => !IsBusy);
        SellCommand = new RelayCommand(async _ => await SellSelectedAsync(), _ => SelectedProduct is not null && !IsBusy);
    }

    public ObservableCollection<ProductDto> Products { get; }

    public RelayCommand SearchCommand { get; }

    public RelayCommand SellCommand { get; }

    public string SearchKeyword
    {
        get => _searchKeyword;
        set => SetProperty(ref _searchKeyword, value);
    }

    public ProductDto? SelectedProduct
    {
        get => _selectedProduct;
        set => SetProperty(ref _selectedProduct, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public decimal TodayRevenue
    {
        get => _todayRevenue;
        private set => SetProperty(ref _todayRevenue, value);
    }

    public int TodayTransactions
    {
        get => _todayTransactions;
        private set => SetProperty(ref _todayTransactions, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    public async Task InitializeAsync()
    {
        await LoadProductsAsync();
        await RefreshTodaySummaryAsync();
    }

    private async Task LoadProductsAsync()
    {
        IsBusy = true;
        try
        {
            var products = await _productService.SearchAsync(SearchKeyword);

            Products.Clear();
            foreach (var product in products)
                Products.Add(product);

            StatusMessage = $"{Products.Count} produk ditampilkan.";
        }
        catch (Exception exception)
        {
            StatusMessage = $"Gagal memuat produk: {exception.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task SellSelectedAsync()
    {
        if (SelectedProduct is null)
            return;

        IsBusy = true;
        try
        {
            var request = new CheckoutRequest(
                new[] { new CheckoutItemRequest(SelectedProduct.Id, 1) },
                PaymentMethod.Cash);

            var sale = await _saleService.CheckoutAsync(request);

            StatusMessage = $"Transaksi {sale.InvoiceNumber} berhasil - total Rp {sale.TotalAmount:N0}.";

            await LoadProductsAsync();
            await RefreshTodaySummaryAsync();
        }
        catch (Exception exception)
        {
            StatusMessage = $"Transaksi gagal: {exception.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RefreshTodaySummaryAsync()
    {
        TodayRevenue = await _saleService.GetTodayRevenueAsync();

        var sales = await _saleService.GetTodaySalesAsync();
        TodayTransactions = sales.Count;
    }
}
