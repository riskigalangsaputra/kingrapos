using KingraPOS.Application.Dtos;

namespace KingraPOS.Application.Abstractions.Services;

public interface ISupplierService
{
    Task<IReadOnlyList<SupplierDto>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<SupplierDto> CreateAsync(SupplierRequest request, CancellationToken cancellationToken = default);

    Task<SupplierDto> UpdateAsync(
        string id,
        SupplierRequest request,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(string id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SupplierContactDto>> GetContactsAsync(
        string supplierId,
        CancellationToken cancellationToken = default);

    Task<SupplierContactDto> AddContactAsync(
        string supplierId,
        SupplierContactRequest request,
        CancellationToken cancellationToken = default);

    Task UpdateContactAsync(
        string contactId,
        SupplierContactRequest request,
        CancellationToken cancellationToken = default);

    Task RemoveContactAsync(string contactId, CancellationToken cancellationToken = default);
}
