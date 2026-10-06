using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentValidation;
using KingraPOS.Application.Abstractions.Services;
using KingraPOS.Application.Dtos;
using KingraPOS.Application.Security;

namespace KingraPOS.UI.ViewModels.Pages;

public partial class TierEditRow : ObservableObject
{
    [ObservableProperty]
    private string _tierName;

    [ObservableProperty]
    private long _minimumQuantity;

    [ObservableProperty]
    private long _tierPrice;

    public TierEditRow(string tierName, long minimumQuantity, long tierPrice)
    {
        _tierName = tierName;
        _minimumQuantity = minimumQuantity;
        _tierPrice = tierPrice;
    }

    public ProductPriceTierRequest ToRequest() => new(TierName, MinimumQuantity, TierPrice);
}

public partial class ProductsViewModel : ObservableObject
{
    private readonly IProductService _productService;
    private readonly ICategoryService _categoryService;
    private readonly IUnitService _unitService;
    private readonly IPermissionService _permissionService;
    private readonly IValidator<CreateProductRequest> _createValidator;
    private readonly IValidator<ProductUnitRequest> _unitValidator;
    private readonly IValidator<IReadOnlyCollection<ProductPriceTierRequest>> _tierValidator;

    [ObservableProperty]
    private string? _keyword;

    [ObservableProperty]
    private ProductSummaryDto? _selectedProduct;

    [ObservableProperty]
    private ProductDetailDto? _detail;

    [ObservableProperty]
    private string _sku = string.Empty;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private CategoryDto? _selectedCategory;

    [ObservableProperty]
    private string? _description;

    [ObservableProperty]
    private long? _baseCostPrice;

    [ObservableProperty]
    private long? _defaultServiceFee;

    [ObservableProperty]
    private bool _trackStock = true;

    [ObservableProperty]
    private bool _isActive = true;

    [ObservableProperty]
    private UnitDto? _baseUnit;

    [ObservableProperty]
    private ProductUnitDetailDto? _selectedUnit;

    [ObservableProperty]
    private long _unitPrice;

    [ObservableProperty]
    private UnitDto? _newUnit;

    [ObservableProperty]
    private string? _newUnitBarcode;

    [ObservableProperty]
    private long _newUnitConversionFactor = 1000;

    [ObservableProperty]
    private TierEditRow? _selectedTier;

    [ObservableProperty]
    private string? _message;

    [ObservableProperty]
    private string? _unitMessage;

    [ObservableProperty]
    private bool _isBusy;

    public ProductsViewModel(
        IProductService productService,
        ICategoryService categoryService,
        IUnitService unitService,
        IPermissionService permissionService,
        IValidator<CreateProductRequest> createValidator,
        IValidator<ProductUnitRequest> unitValidator,
        IValidator<IReadOnlyCollection<ProductPriceTierRequest>> tierValidator)
    {
        _productService = productService;
        _categoryService = categoryService;
        _unitService = unitService;
        _permissionService = permissionService;
        _createValidator = createValidator;
        _unitValidator = unitValidator;
        _tierValidator = tierValidator;
    }

    public ObservableCollection<ProductSummaryDto> Products { get; } = new();

    public ObservableCollection<CategoryDto> Categories { get; } = new();

    public ObservableCollection<UnitDto> Units { get; } = new();

    public ObservableCollection<TierEditRow> Tiers { get; } = new();

    public ObservableCollection<ProductPriceHistoryDto> PriceHistory { get; } = new();

    public bool CanCreate => _permissionService.HasPermission(PermissionCatalog.ProductCreate);

    public bool CanUpdate => _permissionService.HasPermission(PermissionCatalog.ProductUpdate);

    public bool CanDelete => _permissionService.HasPermission(PermissionCatalog.ProductDelete);

    public bool CanManagePrice => _permissionService.HasPermission(PermissionCatalog.ProductManagePrice);

    public bool CanViewCostPrice => _permissionService.HasPermission(PermissionCatalog.ProductViewCostPrice);

    public async Task InitializeAsync()
    {
        IsBusy = true;

        try
        {
            var categories = await _categoryService.GetAllAsync();
            Categories.Clear();
            foreach (var category in categories)
                Categories.Add(category);

            var units = await _unitService.GetAllAsync();
            Units.Clear();
            foreach (var unit in units)
                Units.Add(unit);

            BaseUnit ??= Units.FirstOrDefault();

            await SearchAsync();
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
    private async Task SearchAsync()
    {
        IsBusy = true;

        try
        {
            var products = await _productService.SearchAsync(Keyword);

            Products.Clear();
            foreach (var product in products)
                Products.Add(product);
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
    private void New()
    {
        SelectedProduct = null;
        Detail = null;
        Sku = string.Empty;
        Name = string.Empty;
        SelectedCategory = null;
        Description = null;
        BaseCostPrice = null;
        DefaultServiceFee = null;
        TrackStock = true;
        IsActive = true;

        Tiers.Clear();
        PriceHistory.Clear();
        UnitMessage = null;
        Message = null;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        Message = null;

        IsBusy = true;

        try
        {
            if (SelectedProduct is null)
            {
                var request = new CreateProductRequest(
                    Sku,
                    Name,
                    SelectedCategory?.Id,
                    BaseUnit?.Id ?? string.Empty,
                    Description,
                    CanViewCostPrice ? BaseCostPrice : null,
                    DefaultServiceFee,
                    TrackStock);

                var validation = await _createValidator.ValidateAsync(request);
                if (!validation.IsValid)
                {
                    Message = string.Join(Environment.NewLine, validation.Errors.Select(error => error.ErrorMessage));
                    return;
                }

                var created = await _productService.CreateAsync(request);
                Message = $"Produk '{created.Name}' dibuat.";

                await SearchAsync();
                SelectedProduct = Products.FirstOrDefault(product => product.Id == created.Id);
            }
            else
            {
                var request = new UpdateProductRequest(
                    Sku,
                    Name,
                    SelectedCategory?.Id,
                    Description,
                    CanViewCostPrice ? BaseCostPrice : null,
                    DefaultServiceFee,
                    TrackStock,
                    IsActive);

                var updated = await _productService.UpdateAsync(SelectedProduct.Id, request);
                Message = $"Produk '{updated.Name}' diperbarui.";

                await SearchAsync();
            }
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
    private async Task DeleteAsync()
    {
        if (Detail is null)
        {
            Message = "Pilih produk terlebih dahulu.";
            return;
        }

        IsBusy = true;

        try
        {
            await _productService.DeleteAsync(Detail.Id);
            Message = $"Produk '{Detail.Name}' dihapus.";

            New();
            await SearchAsync();
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
    private async Task AddUnitAsync()
    {
        UnitMessage = null;

        if (Detail is null)
        {
            UnitMessage = "Pilih produk terlebih dahulu.";
            return;
        }

        if (NewUnit is null)
        {
            UnitMessage = "Pilih satuan terlebih dahulu.";
            return;
        }

        var request = new ProductUnitRequest(NewUnit.Id, NewUnitBarcode, NewUnitConversionFactor);

        var validation = await _unitValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            UnitMessage = string.Join(Environment.NewLine, validation.Errors.Select(error => error.ErrorMessage));
            return;
        }

        try
        {
            await _productService.AddUnitAsync(Detail.Id, request);
            UnitMessage = $"Satuan '{NewUnit.Name}' ditambahkan.";

            NewUnitBarcode = null;
            NewUnitConversionFactor = 1000;

            await ReloadDetailAsync();
        }
        catch (Exception exception)
        {
            UnitMessage = exception.Message;
        }
    }

    [RelayCommand]
    private async Task RemoveUnitAsync()
    {
        if (SelectedUnit is null)
        {
            UnitMessage = "Pilih satuan jual terlebih dahulu.";
            return;
        }

        try
        {
            await _productService.RemoveUnitAsync(SelectedUnit.Id);
            UnitMessage = "Satuan jual dihapus.";

            await ReloadDetailAsync();
        }
        catch (Exception exception)
        {
            UnitMessage = exception.Message;
        }
    }

    [RelayCommand]
    private async Task SavePriceAsync()
    {
        UnitMessage = null;

        if (SelectedUnit is null)
        {
            UnitMessage = "Pilih satuan jual terlebih dahulu.";
            return;
        }

        try
        {
            await _productService.SetUnitPriceAsync(SelectedUnit.Id, UnitPrice);
            UnitMessage = $"Harga jual disimpan ({UnitPrice:N0}).";

            await ReloadDetailAsync();
        }
        catch (Exception exception)
        {
            UnitMessage = exception.Message;
        }
    }

    [RelayCommand]
    private void AddTier()
    {
        Tiers.Add(new TierEditRow("Tier baru", 1000, 0));
    }

    [RelayCommand]
    private void RemoveTier()
    {
        if (SelectedTier is not null)
            Tiers.Remove(SelectedTier);
    }

    [RelayCommand]
    private async Task SaveTiersAsync()
    {
        UnitMessage = null;

        if (SelectedUnit is null)
        {
            UnitMessage = "Pilih satuan jual terlebih dahulu.";
            return;
        }

        var requests = Tiers.Select(row => row.ToRequest()).ToList();

        var validation = await _tierValidator.ValidateAsync(requests);
        if (!validation.IsValid)
        {
            UnitMessage = string.Join(Environment.NewLine, validation.Errors.Select(error => error.ErrorMessage));
            return;
        }

        try
        {
            await _productService.SetTiersAsync(SelectedUnit.Id, requests);
            UnitMessage = $"Harga bertingkat disimpan ({requests.Count} tier).";

            await ReloadDetailAsync();
        }
        catch (Exception exception)
        {
            UnitMessage = exception.Message;
        }
    }

    partial void OnSelectedProductChanged(ProductSummaryDto? value)
    {
        _ = LoadDetailAsync(value);
    }

    partial void OnSelectedUnitChanged(ProductUnitDetailDto? value)
    {
        UnitPrice = value?.SellingPrice ?? 0;

        Tiers.Clear();

        if (value is not null)
        {
            foreach (var tier in value.Tiers)
                Tiers.Add(new TierEditRow(tier.TierName, tier.MinimumQuantity, tier.TierPrice));
        }
    }

    private async Task LoadDetailAsync(ProductSummaryDto? product)
    {
        if (product is null)
            return;

        try
        {
            await ReloadDetailAsync();
        }
        catch (Exception exception)
        {
            Message = exception.Message;
        }
    }

    private async Task ReloadDetailAsync()
    {
        if (SelectedProduct is null)
            return;

        var detail = await _productService.GetDetailAsync(SelectedProduct.Id);

        Detail = detail;
        Sku = detail.Sku;
        Name = detail.Name;
        Description = detail.Description;
        BaseCostPrice = detail.BaseCostPrice;
        DefaultServiceFee = detail.DefaultServiceFee;
        TrackStock = detail.TrackStock;
        IsActive = detail.IsActive;
        SelectedCategory = Categories.FirstOrDefault(category => category.Id == detail.CategoryId);

        var previousUnitId = SelectedUnit?.Id;
        SelectedUnit = detail.Units.FirstOrDefault(unit => unit.Id == previousUnitId) ?? detail.Units.FirstOrDefault();

        var history = await _productService.GetPriceHistoryAsync(detail.Id);
        PriceHistory.Clear();
        foreach (var entry in history)
            PriceHistory.Add(entry);
    }
}
