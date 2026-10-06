using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KingraPOS.Application.Abstractions.Services;
using KingraPOS.Application.Dtos;
using KingraPOS.Application.Security;

namespace KingraPOS.UI.ViewModels.Pages;

public partial class PermissionsViewModel : ObservableObject
{
    private readonly IRoleManagementService _roleManagementService;
    private readonly IUserManagementService _userManagementService;
    private readonly IPermissionService _permissionService;
    private readonly ICurrentUserSession _session;

    [ObservableProperty]
    private RoleDto? _selectedRole;

    [ObservableProperty]
    private UserDto? _selectedUser;

    [ObservableProperty]
    private string? _roleMessage;

    [ObservableProperty]
    private string? _userMessage;

    [ObservableProperty]
    private bool _isBusy;

    public PermissionsViewModel(
        IRoleManagementService roleManagementService,
        IUserManagementService userManagementService,
        IPermissionService permissionService,
        ICurrentUserSession session)
    {
        _roleManagementService = roleManagementService;
        _userManagementService = userManagementService;
        _permissionService = permissionService;
        _session = session;
    }

    public ObservableCollection<RoleDto> Roles { get; } = new();

    public ObservableCollection<UserDto> Users { get; } = new();

    public ObservableCollection<RolePermissionRow> RolePermissions { get; } = new();

    public ObservableCollection<UserPermissionRow> UserPermissions { get; } = new();

    public IReadOnlyList<OverrideOption> OverrideOptions => OverrideOption.All;

    public bool CanManage => _permissionService.HasPermission(PermissionCatalog.RoleManage);

    public async Task InitializeAsync()
    {
        IsBusy = true;

        try
        {
            var roles = await _roleManagementService.GetRolesAsync();
            Roles.Clear();
            foreach (var role in roles)
                Roles.Add(role);

            var users = await _userManagementService.GetUsersAsync();
            Users.Clear();
            foreach (var user in users)
                Users.Add(user);

            SelectedRole = Roles.FirstOrDefault();
            SelectedUser = Users.FirstOrDefault(user => user.Id == _session.User?.UserId) ?? Users.FirstOrDefault();
        }
        catch (Exception exception)
        {
            RoleMessage = exception.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SaveRolePermissionsAsync()
    {
        if (SelectedRole is null)
        {
            RoleMessage = "Pilih role terlebih dahulu.";
            return;
        }

        RoleMessage = null;
        IsBusy = true;

        try
        {
            var keys = RolePermissions
                .Where(row => row.IsSelected)
                .Select(row => row.Key)
                .ToList();

            await _roleManagementService.SetRolePermissionsAsync(SelectedRole.Id, keys);

            RoleMessage = $"Izin role '{SelectedRole.Name}' disimpan ({keys.Count} izin).";
        }
        catch (Exception exception)
        {
            RoleMessage = exception.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SaveUserOverridesAsync()
    {
        if (SelectedUser is null)
        {
            UserMessage = "Pilih pengguna terlebih dahulu.";
            return;
        }

        UserMessage = null;
        IsBusy = true;

        try
        {
            foreach (var row in UserPermissions)
                await _roleManagementService.SetUserOverrideAsync(SelectedUser.Id, row.Key, row.State);

            UserMessage = $"Override izin untuk '{SelectedUser.Name}' disimpan.";
        }
        catch (Exception exception)
        {
            UserMessage = exception.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    partial void OnSelectedRoleChanged(RoleDto? value)
    {
        _ = LoadRolePermissionsAsync(value);
    }

    partial void OnSelectedUserChanged(UserDto? value)
    {
        _ = LoadUserPermissionsAsync(value);
    }

    private async Task LoadRolePermissionsAsync(RoleDto? role)
    {
        if (role is null)
            return;

        try
        {
            var permissions = await _roleManagementService.GetPermissionsAsync();
            var assigned = await _roleManagementService.GetRolePermissionKeysAsync(role.Id);
            var assignedSet = new HashSet<string>(assigned, StringComparer.OrdinalIgnoreCase);

            RolePermissions.Clear();
            foreach (var permission in permissions)
                RolePermissions.Add(new RolePermissionRow(permission, assignedSet.Contains(permission.Key)));

            RoleMessage = $"{assignedSet.Count} dari {permissions.Count} izin aktif untuk role '{role.Name}'.";
        }
        catch (Exception exception)
        {
            RoleMessage = exception.Message;
        }
    }

    private async Task LoadUserPermissionsAsync(UserDto? user)
    {
        if (user is null)
            return;

        try
        {
            var permissions = await _roleManagementService.GetPermissionsAsync();
            var overrides = await _roleManagementService.GetUserOverridesAsync(user.Id);
            var effective = await _permissionService.GetEffectivePermissionsAsync(user.Id);

            var overrideMap = overrides.ToDictionary(row => row.PermissionKey, row => row.State, StringComparer.OrdinalIgnoreCase);

            UserPermissions.Clear();
            foreach (var permission in permissions)
            {
                var state = overrideMap.TryGetValue(permission.Key, out var value)
                    ? value
                    : PermissionOverrideState.Default;

                UserPermissions.Add(new UserPermissionRow(
                    permission,
                    state,
                    effective.Contains(permission.Key)));
            }

            UserMessage = $"{overrides.Count} override khusus untuk '{user.Name}'.";
        }
        catch (Exception exception)
        {
            UserMessage = exception.Message;
        }
    }
}
