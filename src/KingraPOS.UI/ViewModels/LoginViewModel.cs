using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentValidation;
using KingraPOS.Application.Abstractions.Services;
using KingraPOS.Application.Dtos;

namespace KingraPOS.UI.ViewModels;

public partial class LoginViewModel : ObservableObject
{
    private readonly IAuthenticationService _authenticationService;
    private readonly IValidator<LoginRequest> _validator;

    [ObservableProperty]
    private string _username = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSubmit))]
    private bool _isBusy;

    public bool CanSubmit => !IsBusy;

    public LoginViewModel(IAuthenticationService authenticationService, IValidator<LoginRequest> validator)
    {
        _authenticationService = authenticationService;
        _validator = validator;
    }

    public event EventHandler? LoggedIn;

    [RelayCommand]
    private async Task LoginAsync()
    {
        ErrorMessage = null;

        var request = new LoginRequest(Username, Password);

        var validation = await _validator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            ErrorMessage = string.Join(Environment.NewLine, validation.Errors.Select(error => error.ErrorMessage));
            return;
        }

        IsBusy = true;

        try
        {
            var result = await _authenticationService.LoginAsync(request);

            if (!result.Success)
            {
                ErrorMessage = result.ErrorMessage;
                return;
            }

            LoggedIn?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception exception)
        {
            ErrorMessage = exception.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
