namespace KingraPOS.Application.Dtos;

public record CategoryDto(string Id, string Name);

public record CategoryRequest(string Name);

public record UnitDto(string Id, string Name, string? Description, bool AllowDecimal);

public record UnitRequest(string Name, string? Description, bool AllowDecimal);

public record SupplierDto(
    string Id,
    string Name,
    string PhoneNumber,
    string? Email,
    string? Address,
    string? Notes,
    int ContactCount);

public record SupplierRequest(
    string Name,
    string PhoneNumber,
    string? Email,
    string? Address,
    string? Notes);

public record SupplierContactDto(
    string Id,
    string SupplierId,
    string Name,
    string? PhoneNumber,
    string? Position,
    bool IsPrimary);

public record SupplierContactRequest(
    string Name,
    string? PhoneNumber,
    string? Position,
    bool IsPrimary);
