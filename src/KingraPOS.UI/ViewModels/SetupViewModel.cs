using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentValidation;
using KingraPOS.Application.Abstractions.Services;
using KingraPOS.Application.Dtos;
using KingraPOS.UI.Common;

namespace KingraPOS.UI.ViewModels;

public partial class SetupViewModel : ObservableObject
{
    private readonly ISetupService _setupService;
    private readonly IValidator<SetupRequest> _validator;

    [ObservableProperty]
    private string _businessName = string.Empty;

    [ObservableProperty]
    private string _ownerName = string.Empty;

    [ObservableProperty]
    private string? _phoneNumber;

    [ObservableProperty]
    private string? _address;

    [ObservableProperty]
    private string? _taxNumber;

    [ObservableProperty]
    private string? _receiptFooter = "Terima kasih telah berbelanja";

    [ObservableProperty]
    private bool _taxEnabled;

    [ObservableProperty]
    private string _taxName = "PPN";

    [ObservableProperty]
    private double _taxRate = 11.0;

    [ObservableProperty]
    private bool _taxInclusive;

    [ObservableProperty]
    private bool _serviceFeeEnabled;

    [ObservableProperty]
    private long _cashRounding;

    [ObservableProperty]
    private string _invoicePrefix = "INV";

    [ObservableProperty]
    private TimezoneOption _selectedTimezone = TimezoneOption.All[0];

    [ObservableProperty]
    private string _ownerFullName = string.Empty;

    [ObservableProperty]
    private string _ownerUsername = string.Empty;

    [ObservableProperty]
    private string _ownerPassword = string.Empty;

    [ObservableProperty]
    private string _ownerPasswordConfirmation = string.Empty;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSubmit))]
    private bool _isBusy;

    public bool CanSubmit => !IsBusy;

    public SetupViewModel(ISetupService setupService, IValidator<SetupRequest> validator)
    {
        _setupService = setupService;
        _validator = validator;
    }

    public IReadOnlyList<TimezoneOption> Timezones => TimezoneOption.All;

    public event EventHandler? Completed;

    [RelayCommand]
    private async Task SaveAsync()
    {
        ErrorMessage = null;

        if (!string.Equals(OwnerPassword, OwnerPasswordConfirmation, StringComparison.Ordinal))
        {
            ErrorMessage = "Konfirmasi password tidak sama dengan password.";
            return;
        }

        var request = new SetupRequest(
            BusinessName,
            OwnerName,
            PhoneNumber,
            Address,
            TaxNumber,
            ReceiptFooter,
            TaxEnabled,
            TaxName,
            TaxRate,
            TaxInclusive,
            ServiceFeeEnabled,
            CashRounding,
            InvoicePrefix,
            SelectedTimezone.Timezone,
            SelectedTimezone.UtcOffsetMinutes,
            OwnerFullName,
            OwnerUsername,
            OwnerPassword);

        var validation = await _validator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            ErrorMessage = string.Join(Environment.NewLine, validation.Errors.Select(error => error.ErrorMessage));
            return;
        }

        IsBusy = true;

        try
        {
            await _setupService.CompleteAsync(request);
            Completed?.Invoke(this, EventArgs.Empty);
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
