using KingraPOS.Application.Abstractions.Persistence;
using KingraPOS.Application.Abstractions.Security;
using KingraPOS.Application.Abstractions.Services;
using KingraPOS.Application.Dtos;
using KingraPOS.Application.Security;
using KingraPOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace KingraPOS.Application.Services;

public sealed class UnitService : IUnitService
{
    private readonly IKingraPosDbContextFactory _contextFactory;
    private readonly IPermissionService _permissionService;
    private readonly ICurrentUserSession _session;
    private readonly IActivityLogService _activityLogService;
    private readonly ILicenseService _licenseService;
    private readonly IClock _clock;

    public UnitService(
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

    public async Task<IReadOnlyList<UnitDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        using var context = _contextFactory.Create();

        return await context.Units
            .Where(unit => unit.DeletedAt == null)
            .OrderBy(unit => unit.Name)
            .Select(unit => new UnitDto(unit.Id, unit.Name, unit.Description, unit.AllowDecimal))
            .ToListAsync(cancellationToken);
    }

    public async Task<UnitDto> CreateAsync(UnitRequest request, CancellationToken cancellationToken = default)
    {
        EnsurePermission(PermissionCatalog.CategoryManage);
        await _licenseService.EnsureWriteAllowedAsync(cancellationToken);

        using var context = _contextFactory.Create();

        var name = request.Name.Trim();

        if (await context.Units.AnyAsync(unit => unit.DeletedAt == null && unit.Name == name, cancellationToken))
            throw new InvalidOperationException($"Satuan '{name}' sudah ada.");

        var now = _clock.UtcNow;

        var unit = new Unit
        {
            Name = name,
            Description = request.Description,
            AllowDecimal = request.AllowDecimal,
            CreatedAt = now,
            UpdatedAt = now
        };

        context.Units.Add(unit);
        await context.SaveChangesAsync(cancellationToken);

        await _activityLogService.LogAsync(
            "UNIT_CREATED",
            $"Satuan '{name}' dibuat.",
            userId: _session.User?.UserId,
            entityType: "units",
            entityId: unit.Id,
            cancellationToken: cancellationToken);

        return new UnitDto(unit.Id, unit.Name, unit.Description, unit.AllowDecimal);
    }

    public async Task<UnitDto> UpdateAsync(
        string id,
        UnitRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsurePermission(PermissionCatalog.CategoryManage);
        await _licenseService.EnsureWriteAllowedAsync(cancellationToken);

        using var context = _contextFactory.Create();

        var unit = await context.Units
            .FirstOrDefaultAsync(row => row.Id == id && row.DeletedAt == null, cancellationToken)
            ?? throw new InvalidOperationException("Satuan tidak ditemukan.");

        var name = request.Name.Trim();

        var duplicate = await context.Units.AnyAsync(
            row => row.Id != id && row.DeletedAt == null && row.Name == name,
            cancellationToken);

        if (duplicate)
            throw new InvalidOperationException($"Satuan '{name}' sudah ada.");

        unit.Name = name;
        unit.Description = request.Description;
        unit.AllowDecimal = request.AllowDecimal;
        unit.UpdatedAt = _clock.UtcNow;

        await context.SaveChangesAsync(cancellationToken);

        await _activityLogService.LogAsync(
            "UNIT_UPDATED",
            $"Satuan diubah menjadi '{name}'.",
            userId: _session.User?.UserId,
            entityType: "units",
            entityId: unit.Id,
            cancellationToken: cancellationToken);

        return new UnitDto(unit.Id, unit.Name, unit.Description, unit.AllowDecimal);
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        EnsurePermission(PermissionCatalog.CategoryManage);
        await _licenseService.EnsureWriteAllowedAsync(cancellationToken);

        using var context = _contextFactory.Create();

        var unit = await context.Units
            .FirstOrDefaultAsync(row => row.Id == id && row.DeletedAt == null, cancellationToken)
            ?? throw new InvalidOperationException("Satuan tidak ditemukan.");

        var usedAsBase = await context.Products.AnyAsync(
            product => product.BaseUnitId == id && product.DeletedAt == null,
            cancellationToken);

        var usedAsSelling = await context.ProductUnits.AnyAsync(
            productUnit => productUnit.UnitId == id && productUnit.DeletedAt == null,
            cancellationToken);

        if (usedAsBase || usedAsSelling)
            throw new InvalidOperationException(
                $"Satuan '{unit.Name}' masih dipakai oleh produk, jadi tidak bisa dihapus.");

        unit.DeletedAt = _clock.UtcNow;
        unit.UpdatedAt = unit.DeletedAt.Value;

        await context.SaveChangesAsync(cancellationToken);

        await _activityLogService.LogAsync(
            "UNIT_DELETED",
            $"Satuan '{unit.Name}' dihapus.",
            userId: _session.User?.UserId,
            entityType: "units",
            entityId: unit.Id,
            cancellationToken: cancellationToken);
    }

    private void EnsurePermission(string permissionKey)
    {
        if (!_permissionService.HasPermission(permissionKey))
            throw new UnauthorizedAccessException($"Anda tidak memiliki izin '{permissionKey}'.");
    }
}
