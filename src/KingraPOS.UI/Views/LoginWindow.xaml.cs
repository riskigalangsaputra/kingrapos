using System.Windows;
using KingraPOS.UI.ViewModels;
using Wpf.Ui.Controls;

namespace KingraPOS.UI.Views;

public partial class LoginWindow : FluentWindow
{
    private readonly LoginViewModel _viewModel;

    public LoginWindow(LoginViewModel viewModel)
    {
        _viewModel = viewModel;

        InitializeComponent();

        DataContext = viewModel;
        viewModel.LoggedIn += OnLoggedIn;
    }

    private void OnLoggedIn(object? sender, EventArgs e)
    {
        _viewModel.LoggedIn -= OnLoggedIn;

        DialogResult = true;
        Close();
    }

    private void OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        _viewModel.Password = PasswordInput.Password;
    }
}
