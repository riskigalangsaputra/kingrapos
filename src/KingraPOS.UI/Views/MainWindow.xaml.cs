using System.Windows;
using System.Windows.Controls;
using KingraPOS.UI.Common;
using KingraPOS.UI.Services;
using KingraPOS.UI.ViewModels;
using Wpf.Ui.Controls;

namespace KingraPOS.UI.Views;

public partial class MainWindow : FluentWindow
{
    private readonly MainShellViewModel _viewModel;
    private readonly INavigationService _navigationService;

    public MainWindow(MainShellViewModel viewModel, INavigationService navigationService)
    {
        _viewModel = viewModel;
        _navigationService = navigationService;

        InitializeComponent();

        DataContext = viewModel;
        _navigationService.CurrentPageChanged += OnCurrentPageChanged;

        NavigateToFirstPage();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await _viewModel.InitializeAsync();
    }

    private void NavigateToFirstPage()
    {
        var firstItem = _viewModel.MenuItems.FirstOrDefault();
        if (firstItem is null)
            return;

        _navigationService.Navigate(firstItem.PageType);
        MenuList.SelectedIndex = 0;
    }

    private void OnCurrentPageChanged(object? sender, EventArgs e)
    {
        PageContent.Content = _navigationService.CurrentPage;
    }

    private void OnMenuSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (MenuList.SelectedItem is not NavigationItem item)
            return;

        _navigationService.Navigate(item.PageType);
    }
}
