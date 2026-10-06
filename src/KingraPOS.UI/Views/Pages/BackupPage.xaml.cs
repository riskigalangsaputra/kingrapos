using System.Windows;
using System.Windows.Controls;
using KingraPOS.UI.ViewModels.Pages;

namespace KingraPOS.UI.Views.Pages;

public partial class BackupPage : UserControl
{
    private readonly BackupViewModel _viewModel;

    public BackupPage(BackupViewModel viewModel)
    {
        _viewModel = viewModel;

        InitializeComponent();

        DataContext = viewModel;
        viewModel.RestoreCompleted += OnRestoreCompleted;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await _viewModel.InitializeAsync();
    }

    private void OnRestoreCompleted(object? sender, EventArgs e)
    {
        _viewModel.RestoreCompleted -= OnRestoreCompleted;

        System.Windows.Application.Current.Shutdown();
    }
}
