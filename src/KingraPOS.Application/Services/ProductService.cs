using KingraPOS.Application.Abstractions.Persistence;
using KingraPOS.Application.Abstractions.Security;
using KingraPOS.Application.Abstractions.Services;
using KingraPOS.Application.Dtos;
using KingraPOS.Application.Security;
using KingraPOS.Domain.Entities;
using KingraPOS.Domain.Enums;
using KingraPOS.Domain.Pricing;
using Microsoft.EntityFrameworkCore;

namespace KingraPOS.Application.Services;

public sealed class ProductService : IProductService
{
    private const long DefaultLowStockThreshold = 5000;
    private const long BaseUnitConversionFactor = 1000;

    private readonly IKingraPosDbContextFactory _contextFactory;
    private readonly IPermissionService _permissionService;
    private readonly ICurrentUserSession _session;
    private readonly IActivityLogService _activityLogService;
    private readonly ILicenseService _licenseService;
    private readonly IClock _clock;

    public ProductService(
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

    public async Task<IReadOnlyList<ProductSummaryDto>> SearchAsync(
        string? keyword,
        CancellationToken cancellationToken = default)
    {
        using var context = _contextFactory.Create();

        var query = context.Products.Where(product => product.DeletedAt == null);

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            // SQLite menerjemahkan Contains menjadi instr() yang case-sensitive, jadi kedua sisi
            // diturunkan ke huruf kecil agar pencarian tidak peka huruf besar/kecil.
            var term = keyword.Trim().ToLowerInvariant();

            // Id produk yang cocok lewat barcode satuan jual diambil lebih dulu, lalu dipakai
            // sebagai daftar IN — pola ini paling andal diterjemahkan EF pada SQLite.
            var barcodeMatches = await context.ProductUnits
                .Where(unit => unit.DeletedAt == null
                               && unit.Barcode != null
                               && unit.Barcode.ToLower().Contains(term))
                .Select(unit => unit.ProductId)
                .ToListAsync(cancellationToken);

            query = query.Where(product =>
                product.Name.ToLower().Contains(term)
                || product.Sku.ToLower().Contains(term)
                || barcodeMatches.Contains(product.Id));
        }

        var products = await query
            .OrderBy(product => product.Name)
            .Take(500)
            .ToListAsync(cancellationToken);

        var categoryNames = await context.Categories
            .ToDictionaryAsync(category => category.Id, category => category.Name, cancellationToken);

        var unitNames = await context.Units
            .ToDictionaryAsync(unit => unit.Id, unit => unit.Name, cancellationToken);

        var canViewCostPrice = _permissionService.HasPermission(PermissionCatalog.ProductViewCostPrice);

        return products
            .Select(product => new ProductSummaryDto(
                product.Id,
                product.Sku,
                product.Name,
                product.CategoryId is not null && categoryNames.TryGetValue(product.CategoryId, out var categoryName)
                    ? categoryName
                    : null,
                unitNames.GetValueOrDefault(product.BaseUnitId, "-"),
                canViewCostPrice ? product.BaseCostPrice : null,
                product.TrackStock,
                product.IsActive,
                product.IsNewProduct))
            .ToList();
    }

    public async Task<ProductDetailDto> GetDetailAsync(
        string productId,
        CancellationToken cancellationToken = default)
    {
        using var context = _contextFactory.Create();

        var product = await context.Products
            .FirstOrDefaultAsync(row => row.Id == productId && row.DeletedAt == null, cancellationToken)
            ?? throw new InvalidOperationException("Produk tidak ditemukan.");

        var units = await context.ProductUnits
            .Where(unit => unit.ProductId == productId && unit.DeletedAt == null)
            .OrderByDescending(unit => unit.IsBaseUnit)
            .ThenBy(unit => unit.ConversionFactor)
            .ToListAsync(cancellationToken);

        var unitIds = units.Select(unit => unit.Id).ToList();

        var prices = await context.ProductPrices
            .Where(price => unitIds.Contains(price.ProductUnitId) && price.DeletedAt == null)
            .ToListAsync(cancellationToken);

        var priceIds = prices.Select(price => price.Id).ToList();

        var tiers = await context.ProductPriceTiers
            .Where(tier => priceIds.Contains(tier.ProductPriceId) && tier.DeletedAt == null)
            .OrderBy(tier => tier.MinimumQuantity)
            .ToListAsync(cancellationToken);

        var unitNames = await context.Units
            .ToDictionaryAsync(unit => unit.Id, unit => unit.Name, cancellationToken);

        var categoryName = product.CategoryId is null
            ? null
            : await context.Categories
                .Where(category => category.Id == product.CategoryId)
                .Select(category => category.Name)
                .FirstOrDefaultAsync(cancellationToken);

        var trendBadgeDays = await GetPriceTrendBadgeDaysAsync(context, cancellationToken);
        var now = _clock.UtcNow;

        var unitDtos = new List<ProductUnitDetailDto>();

        foreach (var unit in units)
        {
            var price = prices.FirstOrDefault(row => row.ProductUnitId == unit.Id);

            var unitTiers = price is null
                ? new List<ProductPriceTierDto>()
                : tiers
                    .Where(tier => tier.ProductPriceId == price.Id)
                    .Select(tier => new ProductPriceTierDto(
                        tier.Id,
                        tier.TierName,
                        tier.MinimumQuantity,
                        tier.TierPrice))
                    .ToList();

            var showTrendBadge = price is not null
                && price.PriceTrend != PriceTrend.STABLE
                && price.PriceChangedAt is not null
                && trendBadgeDays > 0
                && now - price.PriceChangedAt.Value <= TimeSpan.FromDays(trendBadgeDays);

            unitDtos.Add(new ProductUnitDetailDto(
                unit.Id,
                unit.UnitId,
                unitNames.GetValueOrDefault(unit.UnitId, "-"),
                unit.Barcode,
                unit.ConversionFactor,
                unit.IsBaseUnit,
                price?.SellingPrice ?? 0,
                price?.PriceTrend ?? PriceTrend.STABLE,
                price?.PreviousSellingPrice,
                price?.PriceChangedAt,
                showTrendBadge,
                unitTiers));
        }

        var canViewCostPrice = _permissionService.HasPermission(PermissionCatalog.ProductViewCostPrice);

        return new ProductDetailDto(
            product.Id,
            product.Sku,
            product.Name,
            product.Description,
            product.ImagePath,
            product.CategoryId,
            categoryName,
            product.BaseUnitId,
            unitNames.GetValueOrDefault(product.BaseUnitId, "-"),
            canViewCostPrice ? product.BaseCostPrice : null,
            product.DefaultServiceFee,
            product.TrackStock,
            product.IsNewProduct,
            product.IsActive,
            unitDtos);
    }

    public async Task<ProductDetailDto> CreateAsync(
        CreateProductRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsurePermission(PermissionCatalog.ProductCreate);
        await _licenseService.EnsureWriteAllowedAsync(cancellationToken);

        using var context = _contextFactory.Create();

        var sku = request.Sku.Trim();

        if (await context.Products.AnyAsync(
                product => product.DeletedAt == null && product.Sku == sku,
                cancellationToken))
        {
            throw new InvalidOperationException($"Produk dengan SKU '{sku}' sudah ada.");
        }

        var baseUnit = await context.Units
            .FirstOrDefaultAsync(unit => unit.Id == request.BaseUnitId && unit.DeletedAt == null, cancellationToken)
            ?? throw new InvalidOperationException("Satuan dasar tidak ditemukan.");

        var categoryId = await ResolveCategoryIdAsync(context, request.CategoryId, cancellationToken);

        var now = _clock.UtcNow;
        var canViewCostPrice = _permissionService.HasPermission(PermissionCatalog.ProductViewCostPrice);

        var product = new Product
        {
            Sku = sku,
            Name = request.Name.Trim(),
            CategoryId = categoryId,
            BaseUnitId = baseUnit.Id,
            Description = request.Description,
            BaseCostPrice = canViewCostPrice ? request.BaseCostPrice ?? 0 : 0,
            DefaultServiceFee = request.DefaultServiceFee,
            TrackStock = request.TrackStock,
            IsNewProduct = true,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        context.Products.Add(product);

        var productUnit = new ProductUnit
        {
            ProductId = product.Id,
            UnitId = baseUnit.Id,
            ConversionFactor = BaseUnitConversionFactor,
            IsBaseUnit = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        context.ProductUnits.Add(productUnit);

        context.ProductPrices.Add(new ProductPrice
        {
            ProductUnitId = productUnit.Id,
            SellingPrice = 0,
            PriceTrend = PriceTrend.STABLE,
            CreatedAt = now,
            UpdatedAt = now
        });

        context.Inventories.Add(new Inventory
        {
            ProductId = product.Id,
            StockQuantity = 0,
            RejectQuantity = 0,
            LowStockThreshold = DefaultLowStockThreshold,
            CreatedAt = now,
            UpdatedAt = now
        });

        await context.SaveChangesAsync(cancellationToken);

        await _activityLogService.LogAsync(
            "PRODUCT_CREATED",
            $"Produk '{product.Name}' ({sku}) dibuat.",
            userId: _session.User?.UserId,
            entityType: "products",
            entityId: product.Id,
            cancellationToken: cancellationToken);

        return await GetDetailAsync(product.Id, cancellationToken);
    }

    public async Task<ProductDetailDto> UpdateAsync(
        string productId,
        UpdateProductRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsurePermission(PermissionCatalog.ProductUpdate);
        await _licenseService.EnsureWriteAllowedAsync(cancellationToken);

        using var context = _contextFactory.Create();

        var product = await context.Products
            .FirstOrDefaultAsync(row => row.Id == productId && row.DeletedAt == null, cancellationToken)
            ?? throw new InvalidOperationException("Produk tidak ditemukan.");

        var sku = request.Sku.Trim();

        if (await context.Products.AnyAsync(
                row => row.Id != productId && row.DeletedAt == null && row.Sku == sku,
                cancellationToken))
        {
            throw new InvalidOperationException($"Produk dengan SKU '{sku}' sudah ada.");
        }

        product.Sku = sku;
        product.Name = request.Name.Trim();
        product.CategoryId = await ResolveCategoryIdAsync(context, request.CategoryId, cancellationToken);
        product.Description = request.Description;
        product.DefaultServiceFee = request.DefaultServiceFee;
        product.TrackStock = request.TrackStock;
        product.IsActive = request.IsActive;

        if (_permissionService.HasPermission(PermissionCatalog.ProductViewCostPrice) && request.BaseCostPrice.HasValue)
        {
            var newCost = request.BaseCostPrice.Value;

            if (newCost != product.BaseCostPrice)
            {
                context.ProductPriceChangelogs.Add(new ProductPriceChangelog
                {
                    ProductId = product.Id,
                    UserId = RequireUserId(),
                    PriceType = PriceChangeType.COST,
                    OldPrice = product.BaseCostPrice,
                    NewPrice = newCost,
                    CreatedAt = _clock.UtcNow
                });

                product.BaseCostPrice = newCost;
            }
        }

        product.UpdatedAt = _clock.UtcNow;

        await context.SaveChangesAsync(cancellationToken);

        await _activityLogService.LogAsync(
            "PRODUCT_UPDATED",
            $"Produk '{product.Name}' diperbarui.",
            userId: _session.User?.UserId,
            entityType: "products",
            entityId: product.Id,
            cancellationToken: cancellationToken);

        return await GetDetailAsync(product.Id, cancellationToken);
    }

    public async Task SetActiveAsync(string productId, bool isActive, CancellationToken cancellationToken = default)
    {
        EnsurePermission(PermissionCatalog.ProductUpdate);
        await _licenseService.EnsureWriteAllowedAsync(cancellationToken);

        using var context = _contextFactory.Create();

        var product = await context.Products
            .FirstOrDefaultAsync(row => row.Id == productId && row.DeletedAt == null, cancellationToken)
            ?? throw new InvalidOperationException("Produk tidak ditemukan.");

        product.IsActive = isActive;
        product.UpdatedAt = _clock.UtcNow;

        await context.SaveChangesAsync(cancellationToken);

        await _activityLogService.LogAsync(
            isActive ? "PRODUCT_ACTIVATED" : "PRODUCT_DEACTIVATED",
            $"Produk '{product.Name}' {(isActive ? "diaktifkan" : "dinonaktifkan")}.",
            userId: _session.User?.UserId,
            entityType: "products",
            entityId: product.Id,
            cancellationToken: cancellationToken);
    }

    public async Task DeleteAsync(string productId, CancellationToken cancellationToken = default)
    {
        EnsurePermission(PermissionCatalog.ProductDelete);
        await _licenseService.EnsureWriteAllowedAsync(cancellationToken);

        using var context = _contextFactory.Create();

        var product = await context.Products
            .FirstOrDefaultAsync(row => row.Id == productId && row.DeletedAt == null, cancellationToken)
            ?? throw new InvalidOperationException("Produk tidak ditemukan.");

        var hasHistory = await context.StockMutations.AnyAsync(row => row.ProductId == productId, cancellationToken)
            || await context.StockAdjustmentItems.AnyAsync(row => row.ProductId == productId, cancellationToken)
            || await context.TransactionItems.AnyAsync(row => row.ProductId == productId, cancellationToken)
            || await context.ProductRejectLogs.AnyAsync(row => row.ProductId == productId, cancellationToken);

        if (hasHistory)
        {
            throw new InvalidOperationException(
                $"Produk '{product.Name}' sudah punya riwayat stok atau penjualan, jadi tidak bisa dihapus. "
                + "Nonaktifkan saja agar tetap tersimpan sebagai riwayat.");
        }

        var now = _clock.UtcNow;

        product.DeletedAt = now;
        product.UpdatedAt = now;

        var units = await context.ProductUnits
            .Where(unit => unit.ProductId == productId && unit.DeletedAt == null)
            .ToListAsync(cancellationToken);

        var unitIds = units.Select(unit => unit.Id).ToList();

        var prices = await context.ProductPrices
            .Where(price => unitIds.Contains(price.ProductUnitId) && price.DeletedAt == null)
            .ToListAsync(cancellationToken);

        var priceIds = prices.Select(price => price.Id).ToList();

        var tiers = await context.ProductPriceTiers
            .Where(tier => priceIds.Contains(tier.ProductPriceId) && tier.DeletedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var tier in tiers)
        {
            tier.DeletedAt = now;
            tier.UpdatedAt = now;
        }

        foreach (var price in prices)
        {
            price.DeletedAt = now;
            price.UpdatedAt = now;
        }

        foreach (var unit in units)
        {
            unit.DeletedAt = now;
            unit.UpdatedAt = now;
        }

        await context.SaveChangesAsync(cancellationToken);

        await _activityLogService.LogAsync(
            "PRODUCT_DELETED",
            $"Produk '{product.Name}' dihapus.",
            userId: _session.User?.UserId,
            entityType: "products",
            entityId: product.Id,
            cancellationToken: cancellationToken);
    }

    public async Task<ProductUnitDetailDto> AddUnitAsync(
        string productId,
        ProductUnitRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsurePermission(PermissionCatalog.ProductUpdate);
        await _licenseService.EnsureWriteAllowedAsync(cancellationToken);

        using var context = _contextFactory.Create();

        var product = await context.Products
            .FirstOrDefaultAsync(row => row.Id == productId && row.DeletedAt == null, cancellationToken)
            ?? throw new InvalidOperationException("Produk tidak ditemukan.");

        var unit = await context.Units
            .FirstOrDefaultAsync(row => row.Id == request.UnitId && row.DeletedAt == null, cancellationToken)
            ?? throw new InvalidOperationException("Satuan tidak ditemukan.");

        if (unit.Id == product.BaseUnitId)
            throw new InvalidOperationException("Satuan tersebut sudah menjadi satuan dasar produk ini.");

        if (await context.ProductUnits.AnyAsync(
                row => row.ProductId == productId && row.UnitId == unit.Id && row.DeletedAt == null,
                cancellationToken))
        {
            throw new InvalidOperationException($"Satuan '{unit.Name}' sudah dipakai produk ini.");
        }

        await EnsureBarcodeAvailableAsync(context, request.Barcode, null, cancellationToken);

        var now = _clock.UtcNow;

        var productUnit = new ProductUnit
        {
            ProductId = product.Id,
            UnitId = unit.Id,
            Barcode = NormalizeBarcode(request.Barcode),
            ConversionFactor = request.ConversionFactor,
            IsBaseUnit = false,
            CreatedAt = now,
            UpdatedAt = now
        };

        context.ProductUnits.Add(productUnit);

        context.ProductPrices.Add(new ProductPrice
        {
            ProductUnitId = productUnit.Id,
            SellingPrice = 0,
            PriceTrend = PriceTrend.STABLE,
            CreatedAt = now,
            UpdatedAt = now
        });

        await context.SaveChangesAsync(cancellationToken);

        await _activityLogService.LogAsync(
            "PRODUCT_UNIT_ADDED",
            $"Satuan jual '{unit.Name}' ditambahkan ke produk '{product.Name}'.",
            userId: _session.User?.UserId,
            entityType: "product_units",
            entityId: productUnit.Id,
            cancellationToken: cancellationToken);

        var detail = await GetDetailAsync(product.Id, cancellationToken);

        return detail.Units.Single(row => row.Id == productUnit.Id);
    }

    public async Task<ProductUnitDetailDto> UpdateUnitAsync(
        string productUnitId,
        ProductUnitRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsurePermission(PermissionCatalog.ProductUpdate);
        await _licenseService.EnsureWriteAllowedAsync(cancellationToken);

        using var context = _contextFactory.Create();

        var productUnit = await context.ProductUnits
            .FirstOrDefaultAsync(row => row.Id == productUnitId && row.DeletedAt == null, cancellationToken)
            ?? throw new InvalidOperationException("Satuan jual tidak ditemukan.");

        var unit = await context.Units
            .FirstOrDefaultAsync(row => row.Id == request.UnitId && row.DeletedAt == null, cancellationToken)
            ?? throw new InvalidOperationException("Satuan tidak ditemukan.");

        if (productUnit.UnitId != unit.Id)
            throw new InvalidOperationException("Satuan jual tidak dapat diganti; hapus lalu tambahkan satuan baru.");

        if (productUnit.IsBaseUnit && request.ConversionFactor != BaseUnitConversionFactor)
            throw new InvalidOperationException("Faktor konversi satuan dasar harus tetap 1000.");

        await EnsureBarcodeAvailableAsync(context, request.Barcode, productUnitId, cancellationToken);

        productUnit.Barcode = NormalizeBarcode(request.Barcode);
        productUnit.ConversionFactor = request.ConversionFactor;
        productUnit.UpdatedAt = _clock.UtcNow;

        await context.SaveChangesAsync(cancellationToken);

        await _activityLogService.LogAsync(
            "PRODUCT_UNIT_UPDATED",
            $"Satuan jual '{unit.Name}' diperbarui.",
            userId: _session.User?.UserId,
            entityType: "product_units",
            entityId: productUnit.Id,
            cancellationToken: cancellationToken);

        var detail = await GetDetailAsync(productUnit.ProductId, cancellationToken);

        return detail.Units.Single(row => row.Id == productUnit.Id);
    }

    public async Task RemoveUnitAsync(string productUnitId, CancellationToken cancellationToken = default)
    {
        EnsurePermission(PermissionCatalog.ProductUpdate);
        await _licenseService.EnsureWriteAllowedAsync(cancellationToken);

        using var context = _contextFactory.Create();

        var productUnit = await context.ProductUnits
            .FirstOrDefaultAsync(row => row.Id == productUnitId && row.DeletedAt == null, cancellationToken)
            ?? throw new InvalidOperationException("Satuan jual tidak ditemukan.");

        if (productUnit.IsBaseUnit)
            throw new InvalidOperationException("Satuan dasar tidak dapat dihapus.");

        var referenced = await context.TransactionItems.AnyAsync(
                row => row.ProductUnitId == productUnitId, cancellationToken)
            || await context.StockAdjustmentItems.AnyAsync(row => row.ProductUnitId == productUnitId, cancellationToken)
            || await context.StockMutations.AnyAsync(row => row.ProductUnitId == productUnitId, cancellationToken);

        if (referenced)
            throw new InvalidOperationException("Satuan jual ini sudah dipakai pada riwayat stok atau penjualan.");

        var now = _clock.UtcNow;

        productUnit.DeletedAt = now;
        productUnit.UpdatedAt = now;

        var price = await context.ProductPrices
            .FirstOrDefaultAsync(row => row.ProductUnitId == productUnitId && row.DeletedAt == null, cancellationToken);

        if (price is not null)
        {
            var tiers = await context.ProductPriceTiers
                .Where(tier => tier.ProductPriceId == price.Id && tier.DeletedAt == null)
                .ToListAsync(cancellationToken);

            foreach (var tier in tiers)
            {
                tier.DeletedAt = now;
                tier.UpdatedAt = now;
            }

            price.DeletedAt = now;
            price.UpdatedAt = now;
        }

        await context.SaveChangesAsync(cancellationToken);

        await _activityLogService.LogAsync(
            "PRODUCT_UNIT_REMOVED",
            "Satuan jual dihapus dari produk.",
            userId: _session.User?.UserId,
            entityType: "product_units",
            entityId: productUnit.Id,
            cancellationToken: cancellationToken);
    }

    public async Task SetUnitPriceAsync(
        string productUnitId,
        long sellingPrice,
        CancellationToken cancellationToken = default)
    {
        EnsurePermission(PermissionCatalog.ProductManagePrice);
        await _licenseService.EnsureWriteAllowedAsync(cancellationToken);

        if (sellingPrice < 0)
            throw new InvalidOperationException("Harga jual tidak boleh negatif.");

        using var context = _contextFactory.Create();

        var price = await context.ProductPrices
            .FirstOrDefaultAsync(row => row.ProductUnitId == productUnitId && row.DeletedAt == null, cancellationToken)
            ?? throw new InvalidOperationException("Harga satuan tidak ditemukan.");

        if (price.SellingPrice == sellingPrice)
            return;

        var unit = await context.ProductUnits
            .FirstOrDefaultAsync(row => row.Id == productUnitId, cancellationToken);

        var now = _clock.UtcNow;
        var previousPrice = price.SellingPrice;

        price.PreviousSellingPrice = previousPrice;
        price.SellingPrice = sellingPrice;
        price.PriceTrend = sellingPrice > previousPrice ? PriceTrend.UP : PriceTrend.DOWN;
        price.PriceChangedAt = now;
        price.UpdatedAt = now;

        context.ProductPriceChangelogs.Add(new ProductPriceChangelog
        {
            ProductId = unit?.ProductId,
            ProductUnitId = productUnitId,
            UserId = RequireUserId(),
            PriceType = PriceChangeType.SELLING,
            OldPrice = previousPrice,
            NewPrice = sellingPrice,
            CreatedAt = now
        });

        await context.SaveChangesAsync(cancellationToken);

        await _activityLogService.LogAsync(
            "PRODUCT_PRICE_UPDATED",
            $"Harga jual diubah dari {previousPrice} menjadi {sellingPrice}.",
            userId: _session.User?.UserId,
            entityType: "product_prices",
            entityId: price.Id,
            cancellationToken: cancellationToken);
    }

    public async Task SetTiersAsync(
        string productUnitId,
        IReadOnlyCollection<ProductPriceTierRequest> tiers,
        CancellationToken cancellationToken = default)
    {
        EnsurePermission(PermissionCatalog.ProductManagePrice);
        await _licenseService.EnsureWriteAllowedAsync(cancellationToken);

        using var context = _contextFactory.Create();

        var price = await context.ProductPrices
            .FirstOrDefaultAsync(row => row.ProductUnitId == productUnitId && row.DeletedAt == null, cancellationToken)
            ?? throw new InvalidOperationException("Harga satuan tidak ditemukan.");

        var unit = await context.ProductUnits
            .FirstOrDefaultAsync(row => row.Id == productUnitId, cancellationToken);

        var existing = await context.ProductPriceTiers
            .Where(tier => tier.ProductPriceId == price.Id && tier.DeletedAt == null)
            .ToListAsync(cancellationToken);

        var now = _clock.UtcNow;
        var userId = RequireUserId();
        var desired = tiers.ToList();

        foreach (var tier in existing.Where(row => desired.All(item => item.MinimumQuantity != row.MinimumQuantity)))
        {
            tier.DeletedAt = now;
            tier.UpdatedAt = now;
        }

        foreach (var request in desired)
        {
            var current = existing.FirstOrDefault(row => row.MinimumQuantity == request.MinimumQuantity);

            if (current is null)
            {
                context.ProductPriceTiers.Add(new ProductPriceTier
                {
                    ProductPriceId = price.Id,
                    TierName = request.TierName.Trim(),
                    MinimumQuantity = request.MinimumQuantity,
                    TierPrice = request.TierPrice,
                    CreatedAt = now,
                    UpdatedAt = now
                });

                context.ProductPriceChangelogs.Add(new ProductPriceChangelog
                {
                    ProductId = unit?.ProductId,
                    ProductUnitId = productUnitId,
                    UserId = userId,
                    PriceType = PriceChangeType.TIER,
                    OldPrice = 0,
                    NewPrice = request.TierPrice,
                    Notes = $"Tier baru '{request.TierName.Trim()}' mulai {request.MinimumQuantity}.",
                    CreatedAt = now
                });

                continue;
            }

            if (current.TierPrice == request.TierPrice && current.TierName == request.TierName.Trim())
                continue;

            context.ProductPriceChangelogs.Add(new ProductPriceChangelog
            {
                ProductId = unit?.ProductId,
                ProductUnitId = productUnitId,
                UserId = userId,
                PriceType = PriceChangeType.TIER,
                OldPrice = current.TierPrice,
                NewPrice = request.TierPrice,
                Notes = $"Tier '{request.TierName.Trim()}' minimum {request.MinimumQuantity}.",
                CreatedAt = now
            });

            current.TierName = request.TierName.Trim();
            current.TierPrice = request.TierPrice;
            current.UpdatedAt = now;
        }

        await context.SaveChangesAsync(cancellationToken);

        await _activityLogService.LogAsync(
            "PRODUCT_TIERS_UPDATED",
            $"Harga bertingkat diperbarui ({desired.Count} tier).",
            userId: _session.User?.UserId,
            entityType: "product_prices",
            entityId: price.Id,
            cancellationToken: cancellationToken);
    }

    public async Task<long> ResolvePriceAsync(
        string productUnitId,
        long quantity,
        CancellationToken cancellationToken = default)
    {
        using var context = _contextFactory.Create();

        var price = await context.ProductPrices
            .FirstOrDefaultAsync(row => row.ProductUnitId == productUnitId && row.DeletedAt == null, cancellationToken)
            ?? throw new InvalidOperationException("Harga satuan tidak ditemukan.");

        var tiers = await context.ProductPriceTiers
            .Where(tier => tier.ProductPriceId == price.Id && tier.DeletedAt == null)
            .ToListAsync(cancellationToken);

        return TierPriceResolver.Resolve(price.SellingPrice, tiers, quantity);
    }

    public async Task<IReadOnlyList<ProductPriceHistoryDto>> GetPriceHistoryAsync(
        string productId,
        int take = 50,
        CancellationToken cancellationToken = default)
    {
        using var context = _contextFactory.Create();

        var entries = await context.ProductPriceChangelogs
            .Where(entry => entry.ProductId == productId)
            .OrderByDescending(entry => entry.CreatedAt)
            .Take(take)
            .ToListAsync(cancellationToken);

        var userNames = await context.Users
            .ToDictionaryAsync(user => user.Id, user => user.Name, cancellationToken);

        return entries
            .Select(entry => new ProductPriceHistoryDto(
                entry.Id,
                entry.PriceType,
                entry.OldPrice,
                entry.NewPrice,
                entry.Notes,
                entry.CreatedAt,
                userNames.GetValueOrDefault(entry.UserId)))
            .ToList();
    }

    public async Task<int> RefreshNewProductBadgesAsync(CancellationToken cancellationToken = default)
    {
        using var context = _contextFactory.Create();

        var badgeDays = await context.Settings
            .Where(row => row.Id == "main")
            .Select(row => (int?)row.NewProductBadgeDays)
            .FirstOrDefaultAsync(cancellationToken) ?? 7;

        var now = _clock.UtcNow;
        var cutoff = now.AddDays(-Math.Max(badgeDays, 0));

        var query = context.Products.Where(product => product.IsNewProduct && product.DeletedAt == null);

        // badgeDays = 0 berarti penanda dinonaktifkan, jadi seluruh penanda dibersihkan.
        query = badgeDays <= 0 ? query : query.Where(product => product.CreatedAt < cutoff);

        var products = await query.ToListAsync(cancellationToken);

        foreach (var product in products)
        {
            product.IsNewProduct = false;
            product.UpdatedAt = now;
        }

        if (products.Count > 0)
            await context.SaveChangesAsync(cancellationToken);

        return products.Count;
    }

    private async Task<long> GetPriceTrendBadgeDaysAsync(
        IKingraPosDbContext context,
        CancellationToken cancellationToken)
    {
        var days = await context.Settings
            .Where(row => row.Id == "main")
            .Select(row => (int?)row.PriceTrendBadgeDays)
            .FirstOrDefaultAsync(cancellationToken);

        return days ?? 7;
    }

    private static async Task<string?> ResolveCategoryIdAsync(
        IKingraPosDbContext context,
        string? categoryId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(categoryId))
            return null;

        var exists = await context.Categories
            .AnyAsync(category => category.Id == categoryId && category.DeletedAt == null, cancellationToken);

        if (!exists)
            throw new InvalidOperationException("Kategori tidak ditemukan.");

        return categoryId;
    }

    private static async Task EnsureBarcodeAvailableAsync(
        IKingraPosDbContext context,
        string? barcode,
        string? excludeProductUnitId,
        CancellationToken cancellationToken)
    {
        var normalized = NormalizeBarcode(barcode);

        if (normalized is null)
            return;

        var duplicate = await context.ProductUnits.AnyAsync(
            unit => unit.DeletedAt == null
                    && unit.Barcode == normalized
                    && (excludeProductUnitId == null || unit.Id != excludeProductUnitId),
            cancellationToken);

        if (duplicate)
            throw new InvalidOperationException($"Barcode '{normalized}' sudah dipakai satuan jual lain.");
    }

    private static string? NormalizeBarcode(string? barcode) =>
        string.IsNullOrWhiteSpace(barcode) ? null : barcode.Trim();

    private string RequireUserId() =>
        _session.User?.UserId ?? throw new InvalidOperationException("Sesi pengguna tidak ditemukan.");

    private void EnsurePermission(string permissionKey)
    {
        if (!_permissionService.HasPermission(permissionKey))
            throw new UnauthorizedAccessException($"Anda tidak memiliki izin '{permissionKey}'.");
    }
}
