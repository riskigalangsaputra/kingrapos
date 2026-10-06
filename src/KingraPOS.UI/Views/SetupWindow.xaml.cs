using System.Windows;
using KingraPOS.UI.ViewModels;
using Wpf.Ui.Controls;

namespace KingraPOS.UI.Views;

public partial class SetupWindow : FluentWindow
{
    private readonly SetupViewModel _viewModel;

    public SetupWindow(SetupViewModel viewModel)
    {
        _viewModel = viewModel;

        InitializeComponent();

        DataContext = viewModel;
        viewModel.Completed += OnSetupCompleted;
    }

    private void OnSetupCompleted(object? sender, EventArgs e)
    {
        _viewModel.Completed -= OnSetupCompleted;

        DialogResult = true;
        Close();
    }

    private void OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (ReferenceEquals(sender, PasswordInput))
            _viewModel.OwnerPassword = PasswordInput.Password;
        else if (ReferenceEquals(sender, PasswordConfirmationInput))
            _viewModel.OwnerPasswordConfirmation = PasswordConfirmationInput.Password;
    }
}
