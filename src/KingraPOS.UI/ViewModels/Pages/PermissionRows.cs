using CommunityToolkit.Mvvm.ComponentModel;
using KingraPOS.Application.Dtos;

namespace KingraPOS.UI.ViewModels.Pages;

public partial class RolePermissionRow : ObservableObject
{
    [ObservableProperty]
    private bool _isSelected;

    public RolePermissionRow(PermissionDto permission, bool isSelected)
    {
        Permission = permission;
        _isSelected = isSelected;
    }

    public PermissionDto Permission { get; }

    public string Module => Permission.Module;

    public string Name => Permission.Name;

    public string Key => Permission.Key;
}

public partial class UserPermissionRow : ObservableObject
{
    [ObservableProperty]
    private PermissionOverrideState _state;

    public UserPermissionRow(PermissionDto permission, PermissionOverrideState state, bool isEffective)
    {
        Permission = permission;
        _state = state;
        IsEffective = isEffective;
    }

    public PermissionDto Permission { get; }

    public string Module => Permission.Module;

    public string Name => Permission.Name;

    public string Key => Permission.Key;

    public bool IsEffective { get; }
}

public sealed record OverrideOption(PermissionOverrideState Value, string Name)
{
    public static IReadOnlyList<OverrideOption> All { get; } = new[]
    {
        new OverrideOption(PermissionOverrideState.Default, "Ikut role"),
        new OverrideOption(PermissionOverrideState.Allow, "Izinkan"),
        new OverrideOption(PermissionOverrideState.Deny, "Cabut")
    };
}
