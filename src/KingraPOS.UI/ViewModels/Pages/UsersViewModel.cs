using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentValidation;
using KingraPOS.Application.Abstractions.Services;
using KingraPOS.Application.Dtos;
using KingraPOS.Application.Security;

namespace KingraPOS.UI.ViewModels.Pages;

public partial class UsersViewModel : ObservableObject
{
    private readonly IUserManagementService _userManagementService;
    private readonly IPermissionService _permissionService;
    private readonly IValidator<CreateUserRequest> _validator;

    [ObservableProperty]
    private string _newName = string.Empty;

    [ObservableProperty]
    private string _newUsername = string.Empty;

    [ObservableProperty]
    private string _newPassword = string.Empty;

    [ObservableProperty]
    private string? _newEmployeeCode;

    [ObservableProperty]
    private RoleDto? _selectedRole;

    [ObservableProperty]
    private UserDto? _selectedUser;

    [ObservableProperty]
    private string? _message;

    [ObservableProperty]
    private bool _isBusy;

    public UsersViewModel(
        IUserManagementService userManagementService,
        IPermissionService permissionService,
        IValidator<CreateUserRequest> validator)
    {
        _userManagementService = userManagementService;
        _permissionService = permissionService;
        _validator = validator;
    }

    public ObservableCollection<UserDto> Users { get; } = new();

    public ObservableCollection<RoleDto> Roles { get; } = new();

    public bool CanManageUsers => _permissionService.HasPermission(PermissionCatalog.UserManage);

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
            var users = await _userManagementService.GetUsersAsync();
            Users.Clear();
            foreach (var user in users)
                Users.Add(user);

            if (Roles.Count == 0)
            {
                var roles = await _userManagementService.GetRolesAsync();
                foreach (var role in roles)
                    Roles.Add(role);

                SelectedRole ??= Roles.FirstOrDefault();
            }
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
    private async Task CreateAsync()
    {
        Message = null;

        if (SelectedRole is null)
        {
            Message = "Pilih role terlebih dahulu.";
            return;
        }

        var request = new CreateUserRequest(
            NewName,
            NewUsername,
            NewPassword,
            SelectedRole.Id,
            NewEmployeeCode);

        var validation = await _validator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            Message = string.Join(Environment.NewLine, validation.Errors.Select(error => error.ErrorMessage));
            return;
        }

        IsBusy = true;

        try
        {
            var created = await _userManagementService.CreateUserAsync(request);
            Message = $"User '{created.Username}' berhasil dibuat.";

            NewName = string.Empty;
            NewUsername = string.Empty;
            NewPassword = string.Empty;
            NewEmployeeCode = null;

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
    private async Task ToggleActiveAsync()
    {
        if (SelectedUser is null)
        {
            Message = "Pilih user terlebih dahulu.";
            return;
        }

        Message = null;
        IsBusy = true;

        try
        {
            await _userManagementService.SetUserActiveAsync(SelectedUser.Id, !SelectedUser.IsActive);
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
