using System.Windows;
using System.Windows.Controls;
using KingraPOS.UI.ViewModels.Pages;

namespace KingraPOS.UI.Views.Pages;

public partial class ShiftPage : UserControl
{
    private readonly ShiftViewModel _viewModel;

    public ShiftPage(ShiftViewModel viewModel)
    {
        _viewModel = viewModel;

        InitializeComponent();

        DataContext = viewModel;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await _viewModel.InitializeAsync();
    }

    private void OnPinChanged(object sender, RoutedEventArgs e)
    {
        _viewModel.SwitchPin = PinInput.Password;
    }
}
