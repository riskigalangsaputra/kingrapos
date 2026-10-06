using CommunityToolkit.Mvvm.ComponentModel;
using KingraPOS.Application.Abstractions.Services;
using KingraPOS.Application.Dtos;
using KingraPOS.Application.Security;
using KingraPOS.UI.Common;
using KingraPOS.UI.Views.Pages;
using Wpf.Ui.Controls;

namespace KingraPOS.UI.ViewModels;

public partial class MainShellViewModel : ObservableObject
{
    private readonly IPermissionService _permissionService;
    private readonly ILicenseService _licenseService;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLicenseBannerVisible))]
    private string? _licenseBannerMessage;

    public MainShellViewModel(
        ICurrentUserSession session,
        IPermissionService permissionService,
        ILicenseService licenseService)
    {
        _permissionService = permissionService;
        _licenseService = licenseService;

        UserDisplayName = session.User?.Name ?? "-";
        RoleDisplayName = session.User?.RoleName ?? "-";
        MenuItems = BuildMenu();
    }

    public string UserDisplayName { get; }

    public string RoleDisplayName { get; }

    public IReadOnlyList<NavigationItem> MenuItems { get; }

    public bool IsLicenseBannerVisible => !string.IsNullOrEmpty(LicenseBannerMessage);

    public async Task InitializeAsync()
    {
        var license = await _licenseService.GetStateAsync();

        LicenseBannerMessage = license.AccessLevel switch
        {
            LicenseAccessLevel.Restricted =>
                "Mode terbatas: masa berlaku lisensi sudah habis. Penambahan dan perubahan data diblokir sampai lisensi diaktifkan.",
            LicenseAccessLevel.Grace =>
                $"Masa berlaku lisensi sudah lewat. Masa tenggang berakhir {license.GraceEndsAt?.ToLocalTime():dd MMM yyyy} — segera aktifkan lisensi.",
            _ when license.DaysRemaining is > 0 and <= 7 =>
                $"Masa berlaku lisensi tersisa {license.DaysRemaining} hari.",
            _ => null
        };
    }

    private IReadOnlyList<NavigationItem> BuildMenu()
    {
        var candidates = new List<NavigationItem>
        {
            new("Beranda", SymbolRegular.Home24, typeof(DashboardPage), null),
            new("Kategori", SymbolRegular.Tag24, typeof(CategoriesPage), PermissionCatalog.CategoryManage),
            new("Satuan", SymbolRegular.Ruler24, typeof(UnitsPage), PermissionCatalog.CategoryManage),
            new("Produk", SymbolRegular.BoxMultiple24, typeof(ProductsPage), PermissionCatalog.ProductView),
            new("Supplier", SymbolRegular.Building24, typeof(SuppliersPage), PermissionCatalog.SupplierView),
            new("Stok", SymbolRegular.BoxMultipleArrowRight24, typeof(StockPage), PermissionCatalog.StockView),
            new("Barang Rusak", SymbolRegular.Wrench24, typeof(RejectsPage), PermissionCatalog.StockView),
            new("Pengguna", SymbolRegular.People24, typeof(UsersPage), PermissionCatalog.UserView),
            new("Hak Akses", SymbolRegular.Shield24, typeof(PermissionsPage), PermissionCatalog.RoleManage),
            new("Backup", SymbolRegular.ArrowSync24, typeof(BackupPage), PermissionCatalog.SettingsBackup),
            new("Lisensi", SymbolRegular.Key24, typeof(LicensePage), PermissionCatalog.SettingsLicense)
        };

        return candidates
            .Where(item => item.PermissionKey is null || _permissionService.HasPermission(item.PermissionKey))
            .ToList();
    }
}
