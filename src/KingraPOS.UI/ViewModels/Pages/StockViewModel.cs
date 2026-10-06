using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentValidation;
using KingraPOS.Application.Abstractions.Services;
using KingraPOS.Application.Dtos;
using KingraPOS.Application.Security;
using KingraPOS.Application.Stock;

namespace KingraPOS.UI.ViewModels.Pages;

public sealed record StockKindOption(StockAdjustmentKind Value, string Name)
{
    public static IReadOnlyList<StockKindOption> All { get; } = new[]
    {
        new StockKindOption(StockAdjustmentKind.Opening, "Stok Awal"),
        new StockKindOption(StockAdjustmentKind.StockIn, "Barang Masuk"),
        new StockKindOption(StockAdjustmentKind.InputError, "Salah Input"),
        new StockKindOption(StockAdjustmentKind.StockCount, "Hitung Stok"),
        new StockKindOption(StockAdjustmentKind.Lost, "Hilang"),
        new StockKindOption(StockAdjustmentKind.Other, "Lainnya")
    };
}

public partial class StockViewModel : ObservableObject
{
    private readonly IStockService _stockService;
    private readonly IProductService _productService;
    private readonly ISupplierService _supplierService;
    private readonly IPermissionService _permissionService;
    private readonly IValidator<StockAdjustmentRequest> _validator;

    [ObservableProperty]
    private string? _keyword;

    [ObservableProperty]
    private bool _lowStockOnly;

    [ObservableProperty]
    private InventoryDto? _selectedInventory;

    [ObservableProperty]
    private StockKindOption? _selectedKind;

    [ObservableProperty]
    private ProductUnitDetailDto? _selectedUnit;

    [ObservableProperty]
    private long _quantity;

    [ObservableProperty]
    private long? _costPrice;

    [ObservableProperty]
    private SupplierDto? _selectedSupplier;

    [ObservableProperty]
    private string? _referenceNumber;

    [ObservableProperty]
    private string? _notes;

    [ObservableProperty]
    private long _lowStockThreshold;

    [ObservableProperty]
    private string? _message;

    [ObservableProperty]
    private string? _thresholdMessage;

    [ObservableProperty]
    private bool _isBusy;

    public StockViewModel(
        IStockService stockService,
        IProductService productService,
        ISupplierService supplierService,
        IPermissionService permissionService,
        IValidator<StockAdjustmentRequest> validator)
    {
        _stockService = stockService;
        _productService = productService;
        _supplierService = supplierService;
        _permissionService = permissionService;
        _validator = validator;

        _selectedKind = StockKindOption.All[1];
    }

    public ObservableCollection<InventoryDto> Inventories { get; } = new();

    public ObservableCollection<StockMutationDto> Mutations { get; } = new();

    public ObservableCollection<ProductUnitDetailDto> Units { get; } = new();

    public ObservableCollection<SupplierDto> Suppliers { get; } = new();

    public IReadOnlyList<StockKindOption> Kinds => StockKindOption.All;

    public bool CanUpdate => _permissionService.HasPermission(PermissionCatalog.StockUpdate);

    public bool CanViewHistory => _permissionService.HasPermission(PermissionCatalog.StockViewHistory);

    public string QuantityHint => SelectedKind is null || StockAdjustmentRules.IsAbsoluteQuantity(SelectedKind.Value)
        ? "Jumlah diisi sebagai saldo akhir (bukan selisih)."
        : "Jumlah diisi sebagai tambahan pada saldo saat ini.";

    public async Task InitializeAsync()
    {
        IsBusy = true;

        try
        {
            var suppliers = await _supplierService.GetAllAsync();
            Suppliers.Clear();
            foreach (var supplier in suppliers)
                Suppliers.Add(supplier);

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
            var inventories = await _stockService.GetInventoriesAsync(Keyword, LowStockOnly);

            Inventories.Clear();
            foreach (var inventory in inventories)
                Inventories.Add(inventory);

            if (CanViewHistory)
                await ReloadMutationsAsync();
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
    private async Task SaveAdjustmentAsync()
    {
        Message = null;

        if (SelectedInventory is null)
        {
            Message = "Pilih produk terlebih dahulu.";
            return;
        }

        if (SelectedKind is null)
        {
            Message = "Pilih alasan terlebih dahulu.";
            return;
        }

        var request = new StockAdjustmentRequest(
            SelectedKind.Value,
            new[]
            {
                new StockAdjustmentItemRequest(
                    SelectedInventory.ProductId,
                    SelectedUnit?.Id,
                    Quantity,
                    CostPrice)
            },
            SelectedSupplier?.Id,
            null,
            ReferenceNumber,
            Notes);

        var validation = await _validator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            Message = string.Join(Environment.NewLine, validation.Errors.Select(error => error.ErrorMessage));
            return;
        }

        IsBusy = true;

        try
        {
            await _stockService.RecordAdjustmentAsync(request);
            Message = $"Penyesuaian stok '{SelectedKind.Name}' tersimpan.";

            Quantity = 0;
            CostPrice = null;
            ReferenceNumber = null;
            Notes = null;

            await ReloadAsync();

            var refreshed = Inventories.FirstOrDefault(row => row.ProductId == SelectedInventory.ProductId);
            SelectedInventory = refreshed;
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
    private async Task SaveThresholdAsync()
    {
        if (SelectedInventory is null)
        {
            ThresholdMessage = "Pilih produk terlebih dahulu.";
            return;
        }

        try
        {
            await _stockService.SetLowStockThresholdAsync(SelectedInventory.ProductId, LowStockThreshold);
            ThresholdMessage = "Ambang batas disimpan.";

            await ReloadAsync();
        }
        catch (Exception exception)
        {
            ThresholdMessage = exception.Message;
        }
    }

    partial void OnSelectedInventoryChanged(InventoryDto? value)
    {
        if (value is null)
        {
            Units.Clear();
            return;
        }

        LowStockThreshold = value.LowStockThreshold;
        _ = LoadUnitsAsync(value.ProductId);
    }

    partial void OnSelectedKindChanged(StockKindOption? value)
    {
        OnPropertyChanged(nameof(QuantityHint));
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

    private async Task ReloadMutationsAsync()
    {
        var mutations = await _stockService.GetMutationsAsync(take: 100);

        Mutations.Clear();
        foreach (var mutation in mutations)
            Mutations.Add(mutation);
    }
}
