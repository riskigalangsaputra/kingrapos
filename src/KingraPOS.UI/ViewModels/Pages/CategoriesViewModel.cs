using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentValidation;
using KingraPOS.Application.Abstractions.Services;
using KingraPOS.Application.Dtos;
using KingraPOS.Application.Security;

namespace KingraPOS.UI.ViewModels.Pages;

public partial class CategoriesViewModel : ObservableObject
{
    private readonly ICategoryService _categoryService;
    private readonly IPermissionService _permissionService;
    private readonly IValidator<CategoryRequest> _validator;

    [ObservableProperty]
    private CategoryDto? _selected;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string? _message;

    [ObservableProperty]
    private bool _isBusy;

    public CategoriesViewModel(
        ICategoryService categoryService,
        IPermissionService permissionService,
        IValidator<CategoryRequest> validator)
    {
        _categoryService = categoryService;
        _permissionService = permissionService;
        _validator = validator;
    }

    public ObservableCollection<CategoryDto> Items { get; } = new();

    public bool CanManage => _permissionService.HasPermission(PermissionCatalog.CategoryManage);

    public async Task InitializeAsync() => await ReloadAsync();

    [RelayCommand]
    private async Task ReloadAsync()
    {
        IsBusy = true;

        try
        {
            var items = await _categoryService.GetAllAsync();

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
        Message = null;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        Message = null;

        var request = new CategoryRequest(Name);

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
                var created = await _categoryService.CreateAsync(request);
                Message = $"Kategori '{created.Name}' dibuat.";
            }
            else
            {
                var updated = await _categoryService.UpdateAsync(Selected.Id, request);
                Message = $"Kategori '{updated.Name}' diperbarui.";
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
            Message = "Pilih kategori terlebih dahulu.";
            return;
        }

        Message = null;
        IsBusy = true;

        try
        {
            await _categoryService.DeleteAsync(Selected.Id);
            Message = $"Kategori '{Selected.Name}' dihapus.";

            Selected = null;
            Name = string.Empty;

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

    partial void OnSelectedChanged(CategoryDto? value)
    {
        Name = value?.Name ?? string.Empty;
    }
}
