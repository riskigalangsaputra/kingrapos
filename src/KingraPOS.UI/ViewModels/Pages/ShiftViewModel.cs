using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentValidation;
using KingraPOS.Application.Abstractions.Services;
using KingraPOS.Application.Dtos;
using KingraPOS.Application.Security;

namespace KingraPOS.UI.ViewModels.Pages;

public partial class ShiftViewModel : ObservableObject
{
    private readonly IShiftService _shiftService;
    private readonly IPermissionService _permissionService;
    private readonly IValidator<OpenShiftRequest> _openValidator;
    private readonly IValidator<CloseShiftRequest> _closeValidator;
    private readonly IValidator<SwitchShiftRequest> _switchValidator;

    [ObservableProperty]
    private ShiftDto? _currentShift;

    [ObservableProperty]
    private ShiftDto? _selectedShift;

    [ObservableProperty]
    private long _cashDrawerStart;

    [ObservableProperty]
    private long _physicalCash;

    [ObservableProperty]
    private long _switchNewCashDrawerStart;

    [ObservableProperty]
    private string? _switchPin;

    [ObservableProperty]
    private string? _notes;

    [ObservableProperty]
    private string? _message;

    [ObservableProperty]
    private bool _isBusy;

    public ShiftViewModel(
        IShiftService shiftService,
        IPermissionService permissionService,
        IValidator<OpenShiftRequest> openValidator,
        IValidator<CloseShiftRequest> closeValidator,
        IValidator<SwitchShiftRequest> switchValidator)
    {
        _shiftService = shiftService;
        _permissionService = permissionService;
        _openValidator = openValidator;
        _closeValidator = closeValidator;
        _switchValidator = switchValidator;
    }

    public ObservableCollection<ShiftDto> Shifts { get; } = new();

    public bool CanOpen => _permissionService.HasPermission(PermissionCatalog.ShiftOpen);

    public bool CanClose => _permissionService.HasPermission(PermissionCatalog.ShiftClose);

    public bool CanSwitch => CanOpen && CanClose;

    public bool HasOpenShift => CurrentShift is not null;

    public bool HasNoOpenShift => CurrentShift is null;

    public long ExpectedCash => (CurrentShift?.CashDrawerStart ?? 0) + (CurrentShift?.CashSalesAmount ?? 0);

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
            CurrentShift = CanOpen ? await _shiftService.GetOpenShiftAsync() : null;

            if (CanClose)
            {
                var shifts = await _shiftService.GetRecentAsync();

                Shifts.Clear();
                foreach (var shift in shifts)
                    Shifts.Add(shift);
            }

            PhysicalCash = CurrentShift?.CashDrawerEndPhysical ?? ExpectedCash;
            SwitchNewCashDrawerStart = CurrentShift?.CashDrawerStart ?? 0;

            OnPropertyChanged(nameof(HasOpenShift));
            OnPropertyChanged(nameof(HasNoOpenShift));
            OnPropertyChanged(nameof(ExpectedCash));
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
    private async Task OpenAsync()
    {
        Message = null;

        var request = new OpenShiftRequest(CashDrawerStart, Notes);

        var validation = await _openValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            Message = string.Join(Environment.NewLine, validation.Errors.Select(error => error.ErrorMessage));
            return;
        }

        IsBusy = true;

        try
        {
            CurrentShift = await _shiftService.OpenAsync(request);
            Message = "Shift dibuka.";
            CashDrawerStart = 0;
            Notes = null;

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

    [RelayCommand]
    private async Task CloseAsync()
    {
        Message = null;

        if (CurrentShift is null)
        {
            Message = "Tidak ada shift yang terbuka.";
            return;
        }

        var request = new CloseShiftRequest(CurrentShift.Id, PhysicalCash, Notes);

        var validation = await _closeValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            Message = string.Join(Environment.NewLine, validation.Errors.Select(error => error.ErrorMessage));
            return;
        }

        IsBusy = true;

        try
        {
            var closed = await _shiftService.CloseAsync(request);
            Message = $"Shift ditutup. Selisih kas {closed.DiscrepancyAmount}.";
            Notes = null;

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

    [RelayCommand]
    private async Task SwitchAsync()
    {
        Message = null;

        if (CurrentShift is null)
        {
            Message = "Tidak ada shift yang terbuka untuk diganti.";
            return;
        }

        var request = new SwitchShiftRequest(SwitchNewCashDrawerStart, PhysicalCash, SwitchPin, Notes);

        var validation = await _switchValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            Message = string.Join(Environment.NewLine, validation.Errors.Select(error => error.ErrorMessage));
            return;
        }

        IsBusy = true;

        try
        {
            await _shiftService.SwitchAsync(request);
            Message = "Shift berhasil diganti.";
            SwitchPin = null;
            Notes = null;

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
}
