using KingraPOS.Application.Abstractions.Persistence;
using KingraPOS.Application.Abstractions.Security;
using KingraPOS.Application.Abstractions.Services;
using KingraPOS.Application.Dtos;
using KingraPOS.Application.Security;
using KingraPOS.Application.Stock;
using KingraPOS.Domain.Entities;
using KingraPOS.Domain.Enums;
using KingraPOS.Domain.Inventory;
using Microsoft.EntityFrameworkCore;

namespace KingraPOS.Application.Services;

public sealed class StockService : IStockService
{
    private const long DefaultLowStockThreshold = 5000;
    private const long BaseUnitConversionFactor = 1000;

    private readonly IKingraPosDbContextFactory _contextFactory;
    private readonly IPermissionService _permissionService;
    private readonly ICurrentUserSession _session;
    private readonly IActivityLogService _activityLogService;
    private readonly ILicenseService _licenseService;
    private readonly IClock _clock;

    public StockService(
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

    public async Task<IReadOnlyList<InventoryDto>> GetInventoriesAsync(
        string? keyword = null,
        bool lowStockOnly = false,
        CancellationToken cancellationToken = default)
    {
        EnsurePermission(PermissionCatalog.StockView);

        using var context = _contextFactory.Create();

        var products = await context.Products
            .Where(product => product.DeletedAt == null)
            .ToListAsync(cancellationToken);

        var inventories = await context.Inventories.ToDictionaryAsync(
            inventory => inventory.ProductId,
            inventory => inventory,
            cancellationToken);

        var unitNames = await context.Units
            .ToDictionaryAsync(unit => unit.Id, unit => unit.Name, cancellationToken);

        var term = keyword?.Trim().ToLowerInvariant();

        var result = new List<InventoryDto>();

        foreach (var product in products)
        {
            if (!string.IsNullOrEmpty(term)
                && !product.Name.ToLowerInvariant().Contains(term)
                && !product.Sku.ToLowerInvariant().Contains(term))
            {
                continue;
            }

            result.Add(ToDto(
                product,
                inventories.GetValueOrDefault(product.Id),
                unitNames.GetValueOrDefault(product.BaseUnitId, "-")));
        }

        return lowStockOnly
            ? result.Where(row => row.IsLowStock).ToList()
            : result;
    }

    public async Task<InventoryDto> GetInventoryAsync(
        string productId,
        CancellationToken cancellationToken = default)
    {
        EnsurePermission(PermissionCatalog.StockView);

        using var context = _contextFactory.Create();

        var product = await context.Products
            .FirstOrDefaultAsync(row => row.Id == productId && row.DeletedAt == null, cancellationToken)
            ?? throw new InvalidOperationException("Produk tidak ditemukan.");

        var inventory = await context.Inventories
            .FirstOrDefaultAsync(row => row.ProductId == productId, cancellationToken);

        var unitName = await context.Units
            .Where(unit => unit.Id == product.BaseUnitId)
            .Select(unit => unit.Name)
            .FirstOrDefaultAsync(cancellationToken) ?? "-";

        return ToDto(product, inventory, unitName);
    }

    public async Task SetLowStockThresholdAsync(
        string productId,
        long threshold,
        CancellationToken cancellationToken = default)
    {
        EnsurePermission(PermissionCatalog.StockUpdate);
        await _licenseService.EnsureWriteAllowedAsync(cancellationToken);

        if (threshold < 0)
            throw new InvalidOperationException("Ambang batas stok tidak boleh negatif.");

        using var context = _contextFactory.Create();

        var inventory = await GetOrCreateInventoryAsync(context, productId, cancellationToken);

        inventory.LowStockThreshold = threshold;
        inventory.UpdatedAt = _clock.UtcNow;

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<string> RecordAdjustmentAsync(
        StockAdjustmentRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsurePermission(PermissionCatalog.StockUpdate);

        if (request.Kind == StockAdjustmentKind.StockIn)
            EnsurePermission(PermissionCatalog.StockReceive);

        await _licenseService.EnsureWriteAllowedAsync(cancellationToken);

        if (request.Items.Count == 0)
            throw new InvalidOperationException("Tidak ada item yang dicatat.");

        using var context = _contextFactory.Create();

        var now = _clock.UtcNow;
        var userId = RequireUserId();
        var (reason, mutationType) = StockAdjustmentRules.Map(request.Kind);
        var usesAbsoluteQuantity = StockAdjustmentRules.IsAbsoluteQuantity(request.Kind);
        var canViewCostPrice = _permissionService.HasPermission(PermissionCatalog.ProductViewCostPrice);
        var allowNegativeStock = await AllowsNegativeStockAsync(context, cancellationToken);

        var adjustment = new StockAdjustment
        {
            UserId = userId,
            Reason = reason,
            SupplierId = await ResolveSupplierIdAsync(context, request.SupplierId, cancellationToken),
            SupplierContactId = await ResolveSupplierContactIdAsync(context, request.SupplierContactId, cancellationToken),
            ReceivedByUserId = request.Kind == StockAdjustmentKind.StockIn ? userId : null,
            ReferenceNumber = string.IsNullOrWhiteSpace(request.ReferenceNumber) ? null : request.ReferenceNumber.Trim(),
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            CreatedAt = now
        };

        context.StockAdjustments.Add(adjustment);

        foreach (var item in request.Items)
        {
            var product = await context.Products
                .FirstOrDefaultAsync(row => row.Id == item.ProductId && row.DeletedAt == null, cancellationToken)
                ?? throw new InvalidOperationException($"Produk '{item.ProductId}' tidak ditemukan.");

            if (!product.TrackStock)
            {
                throw new InvalidOperationException(
                    $"Produk '{product.Name}' tidak melacak stok, sehingga saldonya tidak dapat disesuaikan.");
            }

            var inventory = await GetOrCreateInventoryAsync(context, product.Id, cancellationToken);
            var productUnit = await ResolveProductUnitAsync(context, product, item.ProductUnitId, cancellationToken);
            var conversionFactor = productUnit?.ConversionFactor ?? BaseUnitConversionFactor;

            var inputBase = ConvertToBaseUnit(item.Quantity, conversionFactor);
            var before = inventory.StockQuantity;
            var after = usesAbsoluteQuantity ? inputBase : before + inputBase;
            var difference = after - before;

            if (!allowNegativeStock && after < 0)
            {
                throw new InvalidOperationException(
                    $"Stok '{product.Name}' akan menjadi negatif ({after / 1000m:0.###}). "
                    + "Aktifkan izin stok negatif pada pengaturan bila memang diinginkan.");
            }

            var costPrice = item.CostPrice;

            if (costPrice is not null && !canViewCostPrice)
                costPrice = null;

            if (costPrice is not null && !StockAdjustmentRules.UpdatesAverageCost(request.Kind))
                costPrice = null;

            context.StockAdjustmentItems.Add(new StockAdjustmentItem
            {
                StockAdjustmentId = adjustment.Id,
                ProductId = product.Id,
                ProductUnitId = productUnit?.Id,
                QuantityInput = item.Quantity,
                QuantityBefore = before,
                QuantityAfter = after,
                Difference = difference,
                CostPrice = costPrice
            });

            inventory.StockQuantity = after;
            inventory.LastMutationAt = now;
            inventory.UpdatedAt = now;

            if (difference != 0)
            {
                context.StockMutations.Add(new StockMutation
                {
                    ProductId = product.Id,
                    ProductUnitId = productUnit?.Id,
                    SupplierId = adjustment.SupplierId,
                    UserId = userId,
                    MutationType = mutationType,
                    StockBucket = StockBucket.AVAILABLE,
                    Quantity = difference,
                    StockBefore = before,
                    StockAfter = after,
                    ReferenceType = StockReferenceType.STOCK_ADJUSTMENT,
                    ReferenceId = adjustment.Id,
                    Notes = adjustment.Notes,
                    CreatedAt = now
                });
            }

            if (costPrice is not null && difference > 0)
            {
                var costPerBaseUnit = ConvertToBaseUnit(costPrice.Value, conversionFactor);
                var newCost = MovingAverageCostCalculator.Calculate(
                    before,
                    product.BaseCostPrice,
                    difference,
                    costPerBaseUnit);

                if (newCost != product.BaseCostPrice)
                {
                    context.ProductPriceChangelogs.Add(new ProductPriceChangelog
                    {
                        ProductId = product.Id,
                        ProductUnitId = productUnit?.Id,
                        UserId = userId,
                        PriceType = PriceChangeType.COST,
                        OldPrice = product.BaseCostPrice,
                        NewPrice = newCost,
                        Notes = "HPP rata-rata bergerak dari barang masuk.",
                        CreatedAt = now
                    });

                    product.BaseCostPrice = newCost;
                }
            }

            product.UpdatedAt = now;
        }

        await context.SaveChangesAsync(cancellationToken);

        await _activityLogService.LogAsync(
            "STOCK_ADJUSTMENT_RECORDED",
            $"Penyesuaian stok '{request.Kind}' dicatat untuk {request.Items.Count} item.",
            userId: _session.User?.UserId,
            entityType: "stock_adjustments",
            entityId: adjustment.Id,
            cancellationToken: cancellationToken);

        return adjustment.Id;
    }

    public async Task<IReadOnlyList<StockMutationDto>> GetMutationsAsync(
        string? productId = null,
        int take = 100,
        CancellationToken cancellationToken = default)
    {
        EnsurePermission(PermissionCatalog.StockViewHistory);

        using var context = _contextFactory.Create();

        var query = context.StockMutations.AsQueryable();

        if (!string.IsNullOrWhiteSpace(productId))
            query = query.Where(mutation => mutation.ProductId == productId);

        var mutations = await query
            .OrderByDescending(mutation => mutation.CreatedAt)
            .Take(take)
            .ToListAsync(cancellationToken);

        var productNames = await context.Products
            .ToDictionaryAsync(product => product.Id, product => product.Name, cancellationToken);

        var userNames = await context.Users
            .ToDictionaryAsync(user => user.Id, user => user.Name, cancellationToken);

        return mutations
            .Select(mutation => new StockMutationDto(
                mutation.Id,
                mutation.ProductId,
                productNames.GetValueOrDefault(mutation.ProductId, "-"),
                mutation.MutationType,
                mutation.StockBucket,
                mutation.Quantity,
                mutation.StockBefore,
                mutation.StockAfter,
                mutation.ReferenceType,
                mutation.ReferenceId,
                mutation.Notes,
                mutation.CreatedAt,
                userNames.GetValueOrDefault(mutation.UserId)))
            .ToList();
    }

    public async Task<IReadOnlyList<RejectLogDto>> GetRejectLogsAsync(
        bool openOnly = false,
        CancellationToken cancellationToken = default)
    {
        EnsurePermission(PermissionCatalog.StockView);

        using var context = _contextFactory.Create();

        var query = context.ProductRejectLogs.AsQueryable();

        if (openOnly)
            query = query.Where(log => log.Status == RejectStatus.REJECTED_IN_STORE);

        var logs = await query
            .OrderByDescending(log => log.CreatedAt)
            .Take(200)
            .ToListAsync(cancellationToken);

        var productNames = await context.Products
            .ToDictionaryAsync(product => product.Id, product => product.Name, cancellationToken);

        var userNames = await context.Users
            .ToDictionaryAsync(user => user.Id, user => user.Name, cancellationToken);

        return logs
            .Select(log => new RejectLogDto(
                log.Id,
                log.ProductId,
                productNames.GetValueOrDefault(log.ProductId, "-"),
                log.RejectReason,
                log.Status,
                log.Quantity,
                log.CostPriceAtIncident,
                log.TotalLossAmount,
                log.SupplierId,
                log.ResolvedAt,
                log.ResolvedByUserId is null ? null : userNames.GetValueOrDefault(log.ResolvedByUserId),
                log.Notes,
                log.CreatedAt))
            .ToList();
    }

    public async Task<string> RecordRejectAsync(
        RejectRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsurePermission(PermissionCatalog.StockReject);
        await _licenseService.EnsureWriteAllowedAsync(cancellationToken);

        if (request.Quantity <= 0)
            throw new InvalidOperationException("Jumlah barang rusak harus lebih dari nol.");

        if (string.IsNullOrWhiteSpace(request.RejectReason))
            throw new InvalidOperationException("Alasan barang rusak wajib diisi.");

        using var context = _contextFactory.Create();

        var product = await context.Products
            .FirstOrDefaultAsync(row => row.Id == request.ProductId && row.DeletedAt == null, cancellationToken)
            ?? throw new InvalidOperationException("Produk tidak ditemukan.");

        if (!product.TrackStock)
            throw new InvalidOperationException($"Produk '{product.Name}' tidak melacak stok.");

        var inventory = await GetOrCreateInventoryAsync(context, product.Id, cancellationToken);
        var allowNegativeStock = await AllowsNegativeStockAsync(context, cancellationToken);
        var supplierId = await ResolveSupplierIdAsync(context, request.SupplierId, cancellationToken);

        if (!allowNegativeStock && inventory.StockQuantity - request.Quantity < 0)
        {
            throw new InvalidOperationException(
                $"Stok siap jual '{product.Name}' tidak mencukupi untuk dinyatakan rusak.");
        }

        var now = _clock.UtcNow;
        var userId = RequireUserId();

        var log = new ProductRejectLog
        {
            ProductId = product.Id,
            UserId = userId,
            RejectReason = request.RejectReason.Trim(),
            Status = RejectStatus.REJECTED_IN_STORE,
            Quantity = request.Quantity,
            CostPriceAtIncident = product.BaseCostPrice,
            TotalLossAmount = CalculateLoss(product.BaseCostPrice, request.Quantity),
            SupplierId = supplierId,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            CreatedAt = now,
            UpdatedAt = now
        };

        context.ProductRejectLogs.Add(log);

        ApplyRejectMovement(context, product, inventory, log.Id, request.Quantity, now, userId, supplierId, log.Notes);

        await context.SaveChangesAsync(cancellationToken);

        await _activityLogService.LogAsync(
            "PRODUCT_REJECTED",
            $"{request.Quantity / 1000m:0.###} satuan '{product.Name}' dicatat rusak ({log.RejectReason}).",
            userId: _session.User?.UserId,
            entityType: "product_reject_logs",
            entityId: log.Id,
            cancellationToken: cancellationToken);

        return log.Id;
    }

    public async Task ResolveRejectAsync(
        string rejectLogId,
        RejectResolution resolution,
        string? supplierId = null,
        CancellationToken cancellationToken = default)
    {
        EnsurePermission(PermissionCatalog.StockReject);
        await _licenseService.EnsureWriteAllowedAsync(cancellationToken);

        using var context = _contextFactory.Create();

        var log = await context.ProductRejectLogs
            .FirstOrDefaultAsync(row => row.Id == rejectLogId, cancellationToken)
            ?? throw new InvalidOperationException("Catatan barang rusak tidak ditemukan.");

        if (log.Status != RejectStatus.REJECTED_IN_STORE)
            throw new InvalidOperationException("Catatan barang rusak ini sudah diselesaikan sebelumnya.");

        var product = await context.Products
            .FirstOrDefaultAsync(row => row.Id == log.ProductId, cancellationToken)
            ?? throw new InvalidOperationException("Produk tidak ditemukan.");

        var inventory = await GetOrCreateInventoryAsync(context, product.Id, cancellationToken);

        if (inventory.RejectQuantity < log.Quantity)
        {
            throw new InvalidOperationException(
                $"Saldo barang rusak '{product.Name}' tidak mencukupi untuk diselesaikan.");
        }

        var now = _clock.UtcNow;
        var userId = RequireUserId();

        var before = inventory.RejectQuantity;
        var after = before - log.Quantity;

        inventory.RejectQuantity = after;
        inventory.LastMutationAt = now;
        inventory.UpdatedAt = now;

        var resolvedSupplierId = resolution == RejectResolution.ReturnedToSupplier
            ? await ResolveSupplierIdAsync(context, supplierId ?? log.SupplierId, cancellationToken)
            : log.SupplierId;

        context.StockMutations.Add(new StockMutation
        {
            ProductId = product.Id,
            UserId = userId,
            SupplierId = resolvedSupplierId,
            MutationType = StockMutationType.REJECT,
            StockBucket = StockBucket.REJECT,
            Quantity = -log.Quantity,
            StockBefore = before,
            StockAfter = after,
            ReferenceType = StockReferenceType.REJECT_LOG,
            ReferenceId = log.Id,
            Notes = $"Penyelesaian barang rusak: {RejectResolutionMap.ToStatus(resolution)}.",
            CreatedAt = now
        });

        log.Status = RejectResolutionMap.ToStatus(resolution);
        log.ResolvedAt = now;
        log.ResolvedByUserId = userId;
        log.UpdatedAt = now;

        if (resolution == RejectResolution.ReturnedToSupplier && resolvedSupplierId is not null)
            log.SupplierId = resolvedSupplierId;

        await context.SaveChangesAsync(cancellationToken);

        await _activityLogService.LogAsync(
            "PRODUCT_REJECT_RESOLVED",
            $"Barang rusak '{product.Name}' diselesaikan sebagai {log.Status}.",
            userId: _session.User?.UserId,
            entityType: "product_reject_logs",
            entityId: log.Id,
            cancellationToken: cancellationToken);
    }

    private static void ApplyRejectMovement(
        IKingraPosDbContext context,
        Product product,
        Inventory inventory,
        string rejectLogId,
        long quantity,
        DateTimeOffset now,
        string userId,
        string? supplierId,
        string? notes)
    {
        var availableBefore = inventory.StockQuantity;
        var availableAfter = availableBefore - quantity;

        var rejectBefore = inventory.RejectQuantity;
        var rejectAfter = rejectBefore + quantity;

        inventory.StockQuantity = availableAfter;
        inventory.RejectQuantity = rejectAfter;
        inventory.LastMutationAt = now;
        inventory.UpdatedAt = now;

        context.StockMutations.Add(new StockMutation
        {
            ProductId = product.Id,
            UserId = userId,
            SupplierId = supplierId,
            MutationType = StockMutationType.REJECT,
            StockBucket = StockBucket.AVAILABLE,
            Quantity = -quantity,
            StockBefore = availableBefore,
            StockAfter = availableAfter,
            ReferenceType = StockReferenceType.REJECT_LOG,
            ReferenceId = rejectLogId,
            Notes = notes,
            CreatedAt = now
        });

        context.StockMutations.Add(new StockMutation
        {
            ProductId = product.Id,
            UserId = userId,
            SupplierId = supplierId,
            MutationType = StockMutationType.REJECT,
            StockBucket = StockBucket.REJECT,
            Quantity = quantity,
            StockBefore = rejectBefore,
            StockAfter = rejectAfter,
            ReferenceType = StockReferenceType.REJECT_LOG,
            ReferenceId = rejectLogId,
            Notes = notes,
            CreatedAt = now
        });
    }

    private static long CalculateLoss(long costPerBaseUnit, long quantity) =>
        (long)Math.Round((decimal)costPerBaseUnit * quantity / 1000m, MidpointRounding.AwayFromZero);

    internal static long ConvertToBaseUnit(long quantity, long conversionFactor) =>
        (long)Math.Round((decimal)quantity * conversionFactor / 1000m, MidpointRounding.AwayFromZero);

    private static async Task<bool> AllowsNegativeStockAsync(
        IKingraPosDbContext context,
        CancellationToken cancellationToken)
    {
        var allowed = await context.Settings
            .Where(row => row.Id == "main")
            .Select(row => (bool?)row.AllowNegativeStock)
            .FirstOrDefaultAsync(cancellationToken);

        return allowed ?? false;
    }

    private static async Task<string?> ResolveSupplierIdAsync(
        IKingraPosDbContext context,
        string? supplierId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(supplierId))
            return null;

        var exists = await context.Suppliers
            .AnyAsync(supplier => supplier.Id == supplierId && supplier.DeletedAt == null, cancellationToken);

        if (!exists)
            throw new InvalidOperationException("Supplier tidak ditemukan.");

        return supplierId;
    }

    private static async Task<string?> ResolveSupplierContactIdAsync(
        IKingraPosDbContext context,
        string? contactId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(contactId))
            return null;

        var exists = await context.SupplierContacts
            .AnyAsync(contact => contact.Id == contactId && contact.DeletedAt == null, cancellationToken);

        if (!exists)
            throw new InvalidOperationException("Kontak supplier tidak ditemukan.");

        return contactId;
    }

    private static async Task<Inventory> GetOrCreateInventoryAsync(
        IKingraPosDbContext context,
        string productId,
        CancellationToken cancellationToken)
    {
        var inventory = await context.Inventories
            .FirstOrDefaultAsync(row => row.ProductId == productId, cancellationToken);

        if (inventory is not null)
            return inventory;

        var now = DateTimeOffset.UtcNow;

        inventory = new Inventory
        {
            ProductId = productId,
            StockQuantity = 0,
            RejectQuantity = 0,
            LowStockThreshold = DefaultLowStockThreshold,
            CreatedAt = now,
            UpdatedAt = now
        };

        context.Inventories.Add(inventory);

        return inventory;
    }

    private static async Task<ProductUnit?> ResolveProductUnitAsync(
        IKingraPosDbContext context,
        Product product,
        string? productUnitId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(productUnitId))
        {
            return await context.ProductUnits.FirstOrDefaultAsync(
                unit => unit.ProductId == product.Id && unit.IsBaseUnit && unit.DeletedAt == null,
                cancellationToken);
        }

        return await context.ProductUnits.FirstOrDefaultAsync(
            unit => unit.Id == productUnitId && unit.ProductId == product.Id && unit.DeletedAt == null,
            cancellationToken)
            ?? throw new InvalidOperationException($"Satuan jual tersebut bukan milik produk '{product.Name}'.");
    }

    private static InventoryDto ToDto(Product product, Inventory? inventory, string baseUnitName)
    {
        var stock = inventory?.StockQuantity ?? 0;
        var threshold = inventory?.LowStockThreshold ?? DefaultLowStockThreshold;

        return new InventoryDto(
            product.Id,
            product.Sku,
            product.Name,
            baseUnitName,
            stock,
            inventory?.RejectQuantity ?? 0,
            threshold,
            product.TrackStock && stock <= threshold,
            product.TrackStock,
            inventory?.LastMutationAt);
    }

    private string RequireUserId() =>
        _session.User?.UserId ?? throw new InvalidOperationException("Sesi pengguna tidak ditemukan.");

    private void EnsurePermission(string permissionKey)
    {
        if (!_permissionService.HasPermission(permissionKey))
            throw new UnauthorizedAccessException($"Anda tidak memiliki izin '{permissionKey}'.");
    }
}
