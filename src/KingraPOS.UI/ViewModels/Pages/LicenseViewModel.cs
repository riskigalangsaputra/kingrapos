using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentValidation;
using KingraPOS.Application.Abstractions.Services;
using KingraPOS.Application.Dtos;
using KingraPOS.Application.Security;
using KingraPOS.Domain.Enums;

namespace KingraPOS.UI.ViewModels.Pages;

public partial class LicenseViewModel : ObservableObject
{
    private readonly ILicenseService _licenseService;
    private readonly IPermissionService _permissionService;
    private readonly IValidator<LicenseActivationRequest> _validator;

    [ObservableProperty]
    private string _statusText = "-";

    [ObservableProperty]
    private string _accessText = "-";

    [ObservableProperty]
    private string _licensedToText = "-";

    [ObservableProperty]
    private string _keyText = "-";

    [ObservableProperty]
    private string _activatedText = "-";

    [ObservableProperty]
    private string _expiresText = "-";

    [ObservableProperty]
    private string _deviceText = "-";

    [ObservableProperty]
    private string _licenseKeyInput = string.Empty;

    [ObservableProperty]
    private string? _licensedToInput;

    [ObservableProperty]
    private DateTime? _expiresAtInput;

    [ObservableProperty]
    private string? _message;

    [ObservableProperty]
    private bool _isBusy;

    public LicenseViewModel(
        ILicenseService licenseService,
        IPermissionService permissionService,
        IValidator<LicenseActivationRequest> validator)
    {
        _licenseService = licenseService;
        _permissionService = permissionService;
        _validator = validator;
    }

    public bool CanActivate => _permissionService.HasPermission(PermissionCatalog.SettingsLicense);

    public async Task InitializeAsync()
    {
        await ReloadAsync();
    }

    [RelayCommand]
    private async Task ReloadAsync()
    {
        IsBusy = true;

        try
        {
            var state = await _licenseService.GetStateAsync();

            StatusText = DescribeStatus(state.Status);
            AccessText = DescribeAccess(state);
            LicensedToText = state.LicensedTo ?? "Belum ada";
            KeyText = state.MaskedKey ?? "Belum ada";
            ActivatedText = state.ActivatedAt?.ToLocalTime().ToString("dd MMM yyyy HH:mm") ?? "-";
            ExpiresText = state.ExpiresAt is null
                ? "Berlaku selamanya"
                : $"{state.ExpiresAt.Value.ToLocalTime():dd MMM yyyy} (sisa {state.DaysRemaining} hari)";
            DeviceText = string.IsNullOrWhiteSpace(state.DeviceFingerprint) ? "-" : state.DeviceFingerprint!;
        }
        catch (Exception exception)
        {
            Message = exception.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ActivateAsync()
    {
        Message = null;

        var request = new LicenseActivationRequest(
            LicenseKeyInput,
            LicensedToInput,
            ExpiresAtInput is null ? null : DateOnly.FromDateTime(ExpiresAtInput.Value));

        var validation = await _validator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            Message = string.Join(Environment.NewLine, validation.Errors.Select(error => error.ErrorMessage));
            return;
        }

        IsBusy = true;

        try
        {
            var state = await _licenseService.ActivateAsync(request);

            Message = state.ExpiresAt is null
                ? "Lisensi berhasil diaktifkan dan berlaku selamanya."
                : $"Lisensi berhasil diaktifkan sampai {state.ExpiresAt.Value.ToLocalTime():dd MMM yyyy}.";

            LicenseKeyInput = string.Empty;
            LicensedToInput = null;
            ExpiresAtInput = null;

            await ReloadAsync();
        }
        catch (Exception exception)
        {
            Message = exception.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static string DescribeStatus(LicenseStatus status) => status switch
    {
        LicenseStatus.TRIAL => "TRIAL",
        LicenseStatus.ACTIVE => "AKTIF",
        LicenseStatus.EXPIRED => "KEDALUWARSA",
        LicenseStatus.REVOKED => "DICABUT",
        _ => status.ToString()
    };

    private static string DescribeAccess(LicenseStateDto state) => state.AccessLevel switch
    {
        LicenseAccessLevel.Full => "Penuh",
        LicenseAccessLevel.Grace => "Grace period — segera aktifkan lisensi",
        LicenseAccessLevel.Restricted => "Mode terbatas — transaksi dan perubahan data diblokir",
        _ => "-"
    };
}
