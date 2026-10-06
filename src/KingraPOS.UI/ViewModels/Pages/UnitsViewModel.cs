using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentValidation;
using KingraPOS.Application.Abstractions.Services;
using KingraPOS.Application.Dtos;
using KingraPOS.Application.Security;

namespace KingraPOS.UI.ViewModels.Pages;

public partial class UnitsViewModel : ObservableObject
{
    private readonly IUnitService _unitService;
    private readonly IPermissionService _permissionService;
    private readonly IValidator<UnitRequest> _validator;

    [ObservableProperty]
    private UnitDto? _selected;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string? _description;

    [ObservableProperty]
    private bool _allowDecimal;

    [ObservableProperty]
    private string? _message;

    [ObservableProperty]
    private bool _isBusy;

    public UnitsViewModel(
        IUnitService unitService,
        IPermissionService permissionService,
        IValidator<UnitRequest> validator)
    {
        _unitService = unitService;
        _permissionService = permissionService;
        _validator = validator;
    }

    public ObservableCollection<UnitDto> Items { get; } = new();

    public bool CanManage => _permissionService.HasPermission(PermissionCatalog.CategoryManage);

    public async Task InitializeAsync() => await ReloadAsync();

    [RelayCommand]
    private async Task ReloadAsync()
    {
        IsBusy = true;

        try
        {
            var items = await _unitService.GetAllAsync();

            Items.Clear();
            foreach (var item in items)
                Items.Add(item);
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
    private void New()
    {
        Selected = null;
        Name = string.Empty;
        Description = null;
        AllowDecimal = false;
        Message = null;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        Message = null;

        var request = new UnitRequest(Name, Description, AllowDecimal);

        var validation = await _validator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            Message = string.Join(Environment.NewLine, validation.Errors.Select(error => error.ErrorMessage));
            return;
        }

        IsBusy = true;

        try
        {
            if (Selected is null)
            {
                var created = await _unitService.CreateAsync(request);
                Message = $"Satuan '{created.Name}' dibuat.";
            }
            else
            {
                var updated = await _unitService.UpdateAsync(Selected.Id, request);
                Message = $"Satuan '{updated.Name}' diperbarui.";
                Selected = updated;
            }

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
    private async Task DeleteAsync()
    {
        if (Selected is null)
        {
            Message = "Pilih satuan terlebih dahulu.";
            return;
        }

        Message = null;
        IsBusy = true;

        try
        {
            await _unitService.DeleteAsync(Selected.Id);
            Message = $"Satuan '{Selected.Name}' dihapus.";

            New();
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

    partial void OnSelectedChanged(UnitDto? value)
    {
        Name = value?.Name ?? string.Empty;
        Description = value?.Description;
        AllowDecimal = value?.AllowDecimal ?? false;
    }
}
