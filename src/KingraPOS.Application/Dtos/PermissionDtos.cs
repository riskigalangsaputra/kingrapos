namespace KingraPOS.Application.Dtos;

public record PermissionDto(string Key, string Name, string Module);

public enum PermissionOverrideState
{
    Default = 0,
    Allow = 1,
    Deny = 2
}

public record UserOverrideDto(string PermissionKey, PermissionOverrideState State);
