using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentValidation;
using KingraPOS.Application.Abstractions.Services;
using KingraPOS.Application.Dtos;
using KingraPOS.Application.Security;

namespace KingraPOS.UI.ViewModels.Pages;

public partial class SuppliersViewModel : ObservableObject
{
    private readonly ISupplierService _supplierService;
    private readonly IPermissionService _permissionService;
    private readonly IValidator<SupplierRequest> _validator;
    private readonly IValidator<SupplierContactRequest> _contactValidator;

    [ObservableProperty]
    private SupplierDto? _selected;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _phoneNumber = string.Empty;

    [ObservableProperty]
    private string? _email;

    [ObservableProperty]
    private string? _address;

    [ObservableProperty]
    private string? _notes;

    [ObservableProperty]
    private SupplierContactDto? _selectedContact;

    [ObservableProperty]
    private string _contactName = string.Empty;

    [ObservableProperty]
    private string? _contactPhoneNumber;

    [ObservableProperty]
    private string? _contactPosition;

    [ObservableProperty]
    private bool _contactIsPrimary;

    [ObservableProperty]
    private string? _message;

    [ObservableProperty]
    private string? _contactMessage;

    [ObservableProperty]
    private bool _isBusy;

    public SuppliersViewModel(
        ISupplierService supplierService,
        IPermissionService permissionService,
        IValidator<SupplierRequest> validator,
        IValidator<SupplierContactRequest> contactValidator)
    {
        _supplierService = supplierService;
        _permissionService = permissionService;
        _validator = validator;
        _contactValidator = contactValidator;
    }

    public ObservableCollection<SupplierDto> Items { get; } = new();

    public ObservableCollection<SupplierContactDto> Contacts { get; } = new();

    public bool CanManage => _permissionService.HasPermission(PermissionCatalog.SupplierManage);

    public async Task InitializeAsync() => await ReloadAsync();

    [RelayCommand]
    private async Task ReloadAsync()
    {
        IsBusy = true;

        try
        {
            var items = await _supplierService.GetAllAsync();

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
        Message = null;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        Message = null;

        var request = new SupplierRequest(Name, PhoneNumber, Email, Address, Notes);

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
                var created = await _supplierService.CreateAsync(request);
                Message = $"Supplier '{created.Name}' dibuat.";
            }
            else
            {
                var updated = await _supplierService.UpdateAsync(Selected.Id, request);
                Message = $"Supplier '{updated.Name}' diperbarui.";
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
            Message = "Pilih supplier terlebih dahulu.";
            return;
        }

        Message = null;
        IsBusy = true;

        try
        {
            await _supplierService.DeleteAsync(Selected.Id);
            Message = $"Supplier '{Selected.Name}' dihapus.";

            Selected = null;
            Contacts.Clear();

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
    private async Task AddContactAsync()
    {
        if (Selected is null)
        {
            ContactMessage = "Pilih supplier terlebih dahulu.";
            return;
        }

        ContactMessage = null;

        var request = new SupplierContactRequest(ContactName, ContactPhoneNumber, ContactPosition, ContactIsPrimary);

        var validation = await _contactValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            ContactMessage = string.Join(Environment.NewLine, validation.Errors.Select(error => error.ErrorMessage));
            return;
        }

        try
        {
            if (SelectedContact is null)
                await _supplierService.AddContactAsync(Selected.Id, request);
            else
                await _supplierService.UpdateContactAsync(SelectedContact.Id, request);

            ContactMessage = $"Kontak '{request.Name.Trim()}' disimpan.";

            ClearContactForm();
            await LoadContactsAsync(Selected);
        }
        catch (Exception exception)
        {
            ContactMessage = exception.Message;
        }
    }

    [RelayCommand]
    private async Task RemoveContactAsync()
    {
        if (SelectedContact is null)
        {
            ContactMessage = "Pilih kontak terlebih dahulu.";
            return;
        }

        try
        {
            await _supplierService.RemoveContactAsync(SelectedContact.Id);
            ContactMessage = "Kontak dihapus.";

            ClearContactForm();

            if (Selected is not null)
                await LoadContactsAsync(Selected);
        }
        catch (Exception exception)
        {
            ContactMessage = exception.Message;
        }
    }

    [RelayCommand]
    private void NewContact()
    {
        ClearContactForm();
        ContactMessage = null;
    }

    partial void OnSelectedChanged(SupplierDto? value)
    {
        Name = value?.Name ?? string.Empty;
        PhoneNumber = value?.PhoneNumber ?? string.Empty;
        Email = value?.Email;
        Address = value?.Address;
        Notes = value?.Notes;

        ClearContactForm();
        _ = LoadContactsAsync(value);
    }

    partial void OnSelectedContactChanged(SupplierContactDto? value)
    {
        ContactName = value?.Name ?? string.Empty;
        ContactPhoneNumber = value?.PhoneNumber;
        ContactPosition = value?.Position;
        ContactIsPrimary = value?.IsPrimary ?? false;
    }

    private void ClearContactForm()
    {
        SelectedContact = null;
        ContactName = string.Empty;
        ContactPhoneNumber = null;
        ContactPosition = null;
        ContactIsPrimary = false;
    }

    private async Task LoadContactsAsync(SupplierDto? supplier)
    {
        Contacts.Clear();

        if (supplier is null)
            return;

        try
        {
            foreach (var contact in await _supplierService.GetContactsAsync(supplier.Id))
                Contacts.Add(contact);
        }
        catch (Exception exception)
        {
            ContactMessage = exception.Message;
        }
    }
}
