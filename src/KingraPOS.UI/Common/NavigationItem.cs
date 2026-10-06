using Wpf.Ui.Controls;

namespace KingraPOS.UI.Common;

public sealed record NavigationItem(string Title, SymbolRegular Icon, Type PageType, string? PermissionKey);
