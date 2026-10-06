using CommunityToolkit.Mvvm.ComponentModel;
using KingraPOS.Application.Abstractions.Services;
using KingraPOS.Application.Security;
using KingraPOS.UI.Common;
using KingraPOS.UI.Views.Pages;
using Wpf.Ui.Controls;

namespace KingraPOS.UI.ViewModels;

public partial class MainShellViewModel : ObservableObject
{
    private readonly IPermissionService _permissionService;

    public MainShellViewModel(ICurrentUserSession session, IPermissionService permissionService)
    {
        _permissionService = permissionService;

        UserDisplayName = session.User?.Name ?? "-";
        RoleDisplayName = session.User?.RoleName ?? "-";
        MenuItems = BuildMenu();
    }

    public string UserDisplayName { get; }

    public string RoleDisplayName { get; }

    public IReadOnlyList<NavigationItem> MenuItems { get; }

    private IReadOnlyList<NavigationItem> BuildMenu()
    {
        var candidates = new List<NavigationItem>
        {
            new("Beranda", SymbolRegular.Home24, typeof(DashboardPage), null),
            new("Pengguna", SymbolRegular.People24, typeof(UsersPage), PermissionCatalog.UserView)
        };

        return candidates
            .Where(item => item.PermissionKey is null || _permissionService.HasPermission(item.PermissionKey))
            .ToList();
    }
}
