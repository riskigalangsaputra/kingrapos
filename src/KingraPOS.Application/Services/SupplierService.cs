using KingraPOS.Application.Abstractions.Persistence;
using KingraPOS.Application.Abstractions.Security;
using KingraPOS.Application.Abstractions.Services;
using KingraPOS.Application.Dtos;
using KingraPOS.Application.Security;
using KingraPOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace KingraPOS.Application.Services;

public sealed class SupplierService : ISupplierService
{
    private readonly IKingraPosDbContextFactory _contextFactory;
    private readonly IPermissionService _permissionService;
    private readonly ICurrentUserSession _session;
    private readonly IActivityLogService _activityLogService;
    private readonly ILicenseService _licenseService;
    private readonly IClock _clock;

    public SupplierService(
        IKingraPosDbContextFactory contextFactory,
        IPermissionService permissionService,
        ICurrentUserSession session,
        IActivityLogService activityLogService,
        ILicenseService licenseService,
        IClock clock)
    {
        _contextFactory = contextFactory;
        _permissionService = permissionService;
        _session = session;
        _activityLogService = activityLogService;
        _licenseService = licenseService;
        _clock = clock;
    }

    public async Task<IReadOnlyList<SupplierDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        using var context = _contextFactory.Create();

        var suppliers = await context.Suppliers
            .Where(supplier => supplier.DeletedAt == null)
            .OrderBy(supplier => supplier.Name)
            .ToListAsync(cancellationToken);

        var contactCounts = await context.SupplierContacts
            .Where(contact => contact.DeletedAt == null)
            .GroupBy(contact => contact.SupplierId)
            .Select(group => new { SupplierId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(row => row.SupplierId, row => row.Count, cancellationToken);

        return suppliers
            .Select(supplier => ToDto(supplier, contactCounts.GetValueOrDefault(supplier.Id)))
            .ToList();
    }

    public async Task<SupplierDto> CreateAsync(
        SupplierRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureManagePermission();
        await _licenseService.EnsureWriteAllowedAsync(cancellationToken);

        using var context = _contextFactory.Create();

        var name = request.Name.Trim();
        var phoneNumber = request.PhoneNumber.Trim();

        await EnsureNotDuplicateAsync(context, name, phoneNumber, null, cancellationToken);

        var now = _clock.UtcNow;

        var supplier = new Supplier
        {
            Name = name,
            PhoneNumber = phoneNumber,
            Email = request.Email,
            Address = request.Address,
            Notes = request.Notes,
            CreatedAt = now,
            UpdatedAt = now
        };

        context.Suppliers.Add(supplier);
        await context.SaveChangesAsync(cancellationToken);

        await _activityLogService.LogAsync(
            "SUPPLIER_CREATED",
            $"Supplier '{name}' dibuat.",
            userId: _session.User?.UserId,
            entityType: "suppliers",
            entityId: supplier.Id,
            cancellationToken: cancellationToken);

        return ToDto(supplier, 0);
    }

    public async Task<SupplierDto> UpdateAsync(
        string id,
        SupplierRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureManagePermission();
        await _licenseService.EnsureWriteAllowedAsync(cancellationToken);

        using var context = _contextFactory.Create();

        var supplier = await context.Suppliers
            .FirstOrDefaultAsync(row => row.Id == id && row.DeletedAt == null, cancellationToken)
            ?? throw new InvalidOperationException("Supplier tidak ditemukan.");

        var name = request.Name.Trim();
        var phoneNumber = request.PhoneNumber.Trim();

        await EnsureNotDuplicateAsync(context, name, phoneNumber, id, cancellationToken);

        supplier.Name = name;
        supplier.PhoneNumber = phoneNumber;
        supplier.Email = request.Email;
        supplier.Address = request.Address;
        supplier.Notes = request.Notes;
        supplier.UpdatedAt = _clock.UtcNow;

        await context.SaveChangesAsync(cancellationToken);

        await _activityLogService.LogAsync(
            "SUPPLIER_UPDATED",
            $"Supplier diubah menjadi '{name}'.",
            userId: _session.User?.UserId,
            entityType: "suppliers",
            entityId: supplier.Id,
            cancellationToken: cancellationToken);

        var contactCount = await context.SupplierContacts
            .CountAsync(contact => contact.SupplierId == id && contact.DeletedAt == null, cancellationToken);

        return ToDto(supplier, contactCount);
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        EnsureManagePermission();
        await _licenseService.EnsureWriteAllowedAsync(cancellationToken);

        using var context = _contextFactory.Create();

        var supplier = await context.Suppliers
            .FirstOrDefaultAsync(row => row.Id == id && row.DeletedAt == null, cancellationToken)
            ?? throw new InvalidOperationException("Supplier tidak ditemukan.");

        var referenced = await context.StockAdjustments.AnyAsync(row => row.SupplierId == id, cancellationToken)
            || await context.StockMutations.AnyAsync(row => row.SupplierId == id, cancellationToken)
            || await context.ProductRejectLogs.AnyAsync(row => row.SupplierId == id, cancellationToken)
            || await context.Expenses.AnyAsync(row => row.SupplierId == id, cancellationToken);

        if (referenced)
            throw new InvalidOperationException(
                $"Supplier '{supplier.Name}' sudah dipakai pada riwayat stok atau pengeluaran, jadi tidak bisa dihapus.");

        var now = _clock.UtcNow;

        supplier.DeletedAt = now;
        supplier.UpdatedAt = now;

        var contacts = await context.SupplierContacts
            .Where(contact => contact.SupplierId == id && contact.DeletedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var contact in contacts)
        {
            contact.DeletedAt = now;
            contact.UpdatedAt = now;
        }

        await context.SaveChangesAsync(cancellationToken);

        await _activityLogService.LogAsync(
            "SUPPLIER_DELETED",
            $"Supplier '{supplier.Name}' dihapus.",
            userId: _session.User?.UserId,
            entityType: "suppliers",
            entityId: supplier.Id,
            cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<SupplierContactDto>> GetContactsAsync(
        string supplierId,
        CancellationToken cancellationToken = default)
    {
        using var context = _contextFactory.Create();

        return await context.SupplierContacts
            .Where(contact => contact.SupplierId == supplierId && contact.DeletedAt == null)
            .OrderByDescending(contact => contact.IsPrimary)
            .ThenBy(contact => contact.Name)
            .Select(contact => new SupplierContactDto(
                contact.Id,
                contact.SupplierId,
                contact.Name,
                contact.PhoneNumber,
                contact.Position,
                contact.IsPrimary))
            .ToListAsync(cancellationToken);
    }

    public async Task<SupplierContactDto> AddContactAsync(
        string supplierId,
        SupplierContactRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureManagePermission();
        await _licenseService.EnsureWriteAllowedAsync(cancellationToken);

        using var context = _contextFactory.Create();

        var supplier = await context.Suppliers
            .FirstOrDefaultAsync(row => row.Id == supplierId && row.DeletedAt == null, cancellationToken)
            ?? throw new InvalidOperationException("Supplier tidak ditemukan.");

        var now = _clock.UtcNow;

        if (request.IsPrimary)
            await ClearPrimaryContactAsync(context, supplierId, now, cancellationToken);

        var contact = new SupplierContact
        {
            SupplierId = supplier.Id,
            Name = request.Name.Trim(),
            PhoneNumber = request.PhoneNumber,
            Position = request.Position,
            IsPrimary = request.IsPrimary,
            CreatedAt = now,
            UpdatedAt = now
        };

        context.SupplierContacts.Add(contact);
        await context.SaveChangesAsync(cancellationToken);

        await _activityLogService.LogAsync(
            "SUPPLIER_CONTACT_CREATED",
            $"Kontak '{contact.Name}' ditambahkan ke supplier '{supplier.Name}'.",
            userId: _session.User?.UserId,
            entityType: "supplier_contacts",
            entityId: contact.Id,
            cancellationToken: cancellationToken);

        return new SupplierContactDto(
            contact.Id,
            contact.SupplierId,
            contact.Name,
            contact.PhoneNumber,
            contact.Position,
            contact.IsPrimary);
    }

    public async Task UpdateContactAsync(
        string contactId,
        SupplierContactRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureManagePermission();
        await _licenseService.EnsureWriteAllowedAsync(cancellationToken);

        using var context = _contextFactory.Create();

        var contact = await context.SupplierContacts
            .FirstOrDefaultAsync(row => row.Id == contactId && row.DeletedAt == null, cancellationToken)
            ?? throw new InvalidOperationException("Kontak supplier tidak ditemukan.");

        var now = _clock.UtcNow;

        if (request.IsPrimary)
            await ClearPrimaryContactAsync(context, contact.SupplierId, now, cancellationToken);

        contact.Name = request.Name.Trim();
        contact.PhoneNumber = request.PhoneNumber;
        contact.Position = request.Position;
        contact.IsPrimary = request.IsPrimary;
        contact.UpdatedAt = now;

        await context.SaveChangesAsync(cancellationToken);

        await _activityLogService.LogAsync(
            "SUPPLIER_CONTACT_UPDATED",
            $"Kontak '{contact.Name}' diperbarui.",
            userId: _session.User?.UserId,
            entityType: "supplier_contacts",
            entityId: contact.Id,
            cancellationToken: cancellationToken);
    }

    public async Task RemoveContactAsync(string contactId, CancellationToken cancellationToken = default)
    {
        EnsureManagePermission();
        await _licenseService.EnsureWriteAllowedAsync(cancellationToken);

        using var context = _contextFactory.Create();

        var contact = await context.SupplierContacts
            .FirstOrDefaultAsync(row => row.Id == contactId && row.DeletedAt == null, cancellationToken)
            ?? throw new InvalidOperationException("Kontak supplier tidak ditemukan.");

        contact.DeletedAt = _clock.UtcNow;
        contact.UpdatedAt = contact.DeletedAt.Value;

        await context.SaveChangesAsync(cancellationToken);

        await _activityLogService.LogAsync(
            "SUPPLIER_CONTACT_DELETED",
            $"Kontak '{contact.Name}' dihapus.",
            userId: _session.User?.UserId,
            entityType: "supplier_contacts",
            entityId: contact.Id,
            cancellationToken: cancellationToken);
    }

    private static async Task ClearPrimaryContactAsync(
        IKingraPosDbContext context,
        string supplierId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var primaries = await context.SupplierContacts
            .Where(contact => contact.SupplierId == supplierId
                              && contact.IsPrimary
                              && contact.DeletedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var primary in primaries)
        {
            primary.IsPrimary = false;
            primary.UpdatedAt = now;
        }
    }

    private static async Task EnsureNotDuplicateAsync(
        IKingraPosDbContext context,
        string name,
        string phoneNumber,
        string? excludeId,
        CancellationToken cancellationToken)
    {
        var duplicate = await context.Suppliers.AnyAsync(
            supplier => supplier.DeletedAt == null
                        && supplier.Name == name
                        && supplier.PhoneNumber == phoneNumber
                        && (excludeId == null || supplier.Id != excludeId),
            cancellationToken);

        if (duplicate)
            throw new InvalidOperationException($"Supplier '{name}' dengan nomor '{phoneNumber}' sudah ada.");
    }

    private static SupplierDto ToDto(Supplier supplier, int contactCount) => new(
        supplier.Id,
        supplier.Name,
        supplier.PhoneNumber,
        supplier.Email,
        supplier.Address,
        supplier.Notes,
        contactCount);

    private void EnsureManagePermission()
    {
        if (!_permissionService.HasPermission(PermissionCatalog.SupplierManage))
            throw new UnauthorizedAccessException($"Anda tidak memiliki izin '{PermissionCatalog.SupplierManage}'.");
    }
}
