using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KingraPOS.Application.Abstractions.Services;
using KingraPOS.Application.Dtos;
using KingraPOS.Application.Security;
using KingraPOS.Application.Stock;

namespace KingraPOS.UI.ViewModels.Pages;

public partial class RejectsViewModel : ObservableObject
{
    private static readonly string[] SuggestedReasons = { "RUSAK", "CACAT", "EXPIRED", "HILANG" };

    private readonly IStockService _stockService;
    private readonly ISupplierService _supplierService;
    private readonly IPermissionService _permissionService;

    [ObservableProperty]
    private InventoryDto? _selectedProduct;

    [ObservableProperty]
    private long _quantity;

    [ObservableProperty]
    private string _rejectReason = "RUSAK";

    [ObservableProperty]
    private SupplierDto? _selectedSupplier;

    [ObservableProperty]
    private string? _notes;

    [ObservableProperty]
    private RejectLogDto? _selectedLog;

    [ObservableProperty]
    private bool _openOnly = true;

    [ObservableProperty]
    private string? _message;

    [ObservableProperty]
    private bool _isBusy;

    public RejectsViewModel(
        IStockService stockService,
        ISupplierService supplierService,
        IPermissionService permissionService)
    {
        _stockService = stockService;
        _supplierService = supplierService;
        _permissionService = permissionService;
    }

    public ObservableCollection<InventoryDto> Products { get; } = new();

    public ObservableCollection<SupplierDto> Suppliers { get; } = new();

    public ObservableCollection<RejectLogDto> Logs { get; } = new();

    public IReadOnlyList<string> Reasons => SuggestedReasons;

    public bool CanReject => _permissionService.HasPermission(PermissionCatalog.StockReject);

    public async Task InitializeAsync()
    {
        IsBusy = true;

        try
        {
            var suppliers = await _supplierService.GetAllAsync();
            Suppliers.Clear();
            foreach (var supplier in suppliers)
                Suppliers.Add(supplier);

            var products = await _stockService.GetInventoriesAsync();
            Products.Clear();
            foreach (var product in products)
                Products.Add(product);

            await ReloadAsync();
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
    private async Task ReloadAsync()
    {
        IsBusy = true;

        try
        {
            var logs = await _stockService.GetRejectLogsAsync(OpenOnly);

            Logs.Clear();
            foreach (var log in logs)
                Logs.Add(log);
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
    private async Task RecordAsync()
    {
        Message = null;

        if (SelectedProduct is null)
        {
            Message = "Pilih produk terlebih dahulu.";
            return;
        }

        IsBusy = true;

        try
        {
            await _stockService.RecordRejectAsync(new RejectRequest(
                SelectedProduct.ProductId,
                Quantity,
                RejectReason,
                SelectedSupplier?.Id,
                Notes));

            Message = "Barang rusak dicatat.";

            Quantity = 0;
            Notes = null;

            await InitializeAsync();
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
    private async Task ResolveAsync(string resolution)
    {
        if (SelectedLog is null)
        {
            Message = "Pilih catatan barang rusak terlebih dahulu.";
            return;
        }

        Message = null;
        IsBusy = true;

        try
        {
            var parsed = resolution switch
            {
                "RETURNED" => RejectResolution.ReturnedToSupplier,
                _ => RejectResolution.WrittenOff
            };

            await _stockService.ResolveRejectAsync(SelectedLog.Id, parsed, SelectedSupplier?.Id);

            Message = parsed == RejectResolution.ReturnedToSupplier
                ? "Barang rusak ditandai dikembalikan ke supplier."
                : "Barang rusak ditandai dihapus sebagai kerugian.";

            await InitializeAsync();
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
}
