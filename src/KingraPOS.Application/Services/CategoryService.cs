using KingraPOS.Application.Abstractions.Persistence;
using KingraPOS.Application.Abstractions.Security;
using KingraPOS.Application.Abstractions.Services;
using KingraPOS.Application.Dtos;
using KingraPOS.Application.Security;
using KingraPOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace KingraPOS.Application.Services;

public sealed class CategoryService : ICategoryService
{
    private readonly IKingraPosDbContextFactory _contextFactory;
    private readonly IPermissionService _permissionService;
    private readonly ICurrentUserSession _session;
    private readonly IActivityLogService _activityLogService;
    private readonly ILicenseService _licenseService;
    private readonly IClock _clock;

    public CategoryService(
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

    public async Task<IReadOnlyList<CategoryDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        using var context = _contextFactory.Create();

        return await context.Categories
            .Where(category => category.DeletedAt == null)
            .OrderBy(category => category.Name)
            .Select(category => new CategoryDto(category.Id, category.Name))
            .ToListAsync(cancellationToken);
    }

    public async Task<CategoryDto> CreateAsync(
        CategoryRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsurePermission(PermissionCatalog.CategoryManage);
        await _licenseService.EnsureWriteAllowedAsync(cancellationToken);

        using var context = _contextFactory.Create();

        var name = request.Name.Trim();

        if (await context.Categories.AnyAsync(
                category => category.DeletedAt == null && category.Name == name,
                cancellationToken))
        {
            throw new InvalidOperationException($"Kategori '{name}' sudah ada.");
        }

        var now = _clock.UtcNow;

        var category = new Category
        {
            Name = name,
            CreatedAt = now,
            UpdatedAt = now
        };

        context.Categories.Add(category);
        await context.SaveChangesAsync(cancellationToken);

        await _activityLogService.LogAsync(
            "CATEGORY_CREATED",
            $"Kategori '{name}' dibuat.",
            userId: _session.User?.UserId,
            entityType: "categories",
            entityId: category.Id,
            cancellationToken: cancellationToken);

        return new CategoryDto(category.Id, category.Name);
    }

    public async Task<CategoryDto> UpdateAsync(
        string id,
        CategoryRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsurePermission(PermissionCatalog.CategoryManage);
        await _licenseService.EnsureWriteAllowedAsync(cancellationToken);

        using var context = _contextFactory.Create();

        var category = await context.Categories
            .FirstOrDefaultAsync(row => row.Id == id && row.DeletedAt == null, cancellationToken)
            ?? throw new InvalidOperationException("Kategori tidak ditemukan.");

        var name = request.Name.Trim();

        var duplicate = await context.Categories.AnyAsync(
            row => row.Id != id && row.DeletedAt == null && row.Name == name,
            cancellationToken);

        if (duplicate)
            throw new InvalidOperationException($"Kategori '{name}' sudah ada.");

        category.Name = name;
        category.UpdatedAt = _clock.UtcNow;

        await context.SaveChangesAsync(cancellationToken);

        await _activityLogService.LogAsync(
            "CATEGORY_UPDATED",
            $"Kategori diubah menjadi '{name}'.",
            userId: _session.User?.UserId,
            entityType: "categories",
            entityId: category.Id,
            cancellationToken: cancellationToken);

        return new CategoryDto(category.Id, category.Name);
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        EnsurePermission(PermissionCatalog.CategoryManage);
        await _licenseService.EnsureWriteAllowedAsync(cancellationToken);

        using var context = _contextFactory.Create();

        var category = await context.Categories
            .FirstOrDefaultAsync(row => row.Id == id && row.DeletedAt == null, cancellationToken)
            ?? throw new InvalidOperationException("Kategori tidak ditemukan.");

        var used = await context.Products.AnyAsync(
            product => product.CategoryId == id && product.DeletedAt == null,
            cancellationToken);

        if (used)
            throw new InvalidOperationException(
                $"Kategori '{category.Name}' masih dipakai oleh produk aktif, jadi tidak bisa dihapus.");

        category.DeletedAt = _clock.UtcNow;
        category.UpdatedAt = category.DeletedAt.Value;

        await context.SaveChangesAsync(cancellationToken);

        await _activityLogService.LogAsync(
            "CATEGORY_DELETED",
            $"Kategori '{category.Name}' dihapus.",
            userId: _session.User?.UserId,
            entityType: "categories",
            entityId: category.Id,
            cancellationToken: cancellationToken);
    }

    private void EnsurePermission(string permissionKey)
    {
        if (!_permissionService.HasPermission(permissionKey))
            throw new UnauthorizedAccessException($"Anda tidak memiliki izin '{permissionKey}'.");
    }
}
