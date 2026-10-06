using System.Windows;
using System.Windows.Controls;
using KingraPOS.UI.ViewModels.Pages;

namespace KingraPOS.UI.Views.Pages;

public partial class RejectsPage : UserControl
{
    private readonly RejectsViewModel _viewModel;

    public RejectsPage(RejectsViewModel viewModel)
    {
        _viewModel = viewModel;

        InitializeComponent();

        DataContext = viewModel;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await _viewModel.InitializeAsync();
    }
}
