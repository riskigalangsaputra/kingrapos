using KingraPOS.Application.Abstractions.Persistence;
using KingraPOS.Application.Abstractions.Security;
using KingraPOS.Application.Abstractions.Services;
using KingraPOS.Application.Dtos;
using KingraPOS.Application.Sales;
using KingraPOS.Application.Security;
using KingraPOS.Domain.Entities;
using KingraPOS.Domain.Enums;
using KingraPOS.Domain.Pricing;
using KingraPOS.Domain.Sales;
using Microsoft.EntityFrameworkCore;

namespace KingraPOS.Application.Services;

public sealed class TransactionService : ITransactionService
{
    private const long BaseUnitConversionFactor = 1000;
    private const long DefaultLowStockThreshold = 5000;

    private readonly IKingraPosDbContextFactory _contextFactory;
    private readonly IPermissionService _permissionService;
    private readonly ICurrentUserSession _session;
    private readonly IActivityLogService _activityLogService;
    private readonly ILicenseService _licenseService;
    private readonly IClock _clock;

    public TransactionService(
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

    public async Task<SaleQuoteDto> QuoteAsync(
        CheckoutRequest request,
        CancellationToken cancellationToken = default)
    {
        using var context = _contextFactory.Create();

        var settings = await LoadSettingsAsync(context, cancellationToken);
        var computation = await ComputeAsync(context, request, settings, cancellationToken);

        return computation.Quote;
    }

    public async Task<TransactionDto> CheckoutAsync(
        CheckoutRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsurePermission(PermissionCatalog.SalesCreate);
        await _licenseService.EnsureWriteAllowedAsync(cancellationToken);

        using var context = _contextFactory.Create();

        var shift = await context.CashierShifts
            .FirstOrDefaultAsync(row => row.Status == ShiftStatus.OPEN, cancellationToken)
            ?? throw new InvalidOperationException("Belum ada shift yang terbuka. Buka shift terlebih dahulu.");

        var settings = await LoadSettingsAsync(context, cancellationToken);
        var computation = await ComputeAsync(context, request, settings, cancellationToken);

        var quote = computation.Quote;
        var method = PaymentMethods.Normalize(request.PaymentMethod);

        if (!PaymentMethods.IsKnown(method))
            throw new InvalidOperationException($"Metode pembayaran '{request.PaymentMethod}' tidak dikenal.");

        var amountPaid = request.AmountPaid;

        if (PaymentMethods.IsCash(method))
        {
            if (amountPaid < quote.TotalNetAmount)
                throw new InvalidOperationException("Uang bayar kurang dari total transaksi.");
        }
        else if (amountPaid <= 0)
        {
            amountPaid = quote.TotalNetAmount;
        }

        if (amountPaid < quote.TotalNetAmount)
            throw new InvalidOperationException("Uang bayar kurang dari total transaksi.");

        var customerId = await ResolveCustomerIdAsync(context, request.CustomerId, cancellationToken);
        var now = _clock.UtcNow;
        var userId = RequireUserId();

        var transaction = new Transaction
        {
            UserId = userId,
            CustomerId = customerId,
            ShiftId = shift.Id,
            InvoiceNumber = await GenerateInvoiceNumberAsync(context, settings, cancellationToken),
            TotalCostPrice = computation.TotalCostPrice,
            ItemsAmount = quote.ItemsAmount,
            ServiceAmount = quote.ServiceAmount,
            TotalGrossAmount = quote.TotalGrossAmount,
            DiscountAmount = quote.DiscountAmount,
            PointDiscountAmount = quote.PointDiscountAmount,
            TaxRate = quote.TaxRate,
            TaxInclusive = quote.TaxInclusive,
            TaxAmount = quote.TaxAmount,
            RoundingAmount = quote.RoundingAmount,
            TotalNetAmount = quote.TotalNetAmount,
            PaymentMethod = method,
            PaymentProvider = Normalize(request.PaymentProvider),
            PaymentReference = Normalize(request.PaymentReference),
            AmountPaid = amountPaid,
            AmountChange = amountPaid - quote.TotalNetAmount,
            PointsEarned = 0,
            PointsUsed = 0,
            Status = TransactionStatus.COMPLETED,
            Notes = Normalize(request.Notes),
            CreatedAt = now,
            UpdatedAt = now
        };

        context.Transactions.Add(transaction);

        foreach (var computed in computation.Lines)
        {
            computed.Item.TransactionId = transaction.Id;
            context.TransactionItems.Add(computed.Item);

            if (computed.Product is not null && !computed.IsService && computed.Product.TrackStock)
            {
                await ApplySaleStockAsync(
                    context,
                    computed.Product,
                    computed.Item,
                    userId,
                    transaction.Id,
                    now,
                    cancellationToken);
            }
        }

        await context.SaveChangesAsync(cancellationToken);

        await _activityLogService.LogAsync(
            "SALE_COMPLETED",
            $"Transaksi {transaction.InvoiceNumber} tersimpan sebesar {transaction.TotalNetAmount}.",
            userId: _session.User?.UserId,
            entityType: "transactions",
            entityId: transaction.Id,
            cancellationToken: cancellationToken);

        return await MapTransactionAsync(context, transaction, cancellationToken);
    }

    public async Task<IReadOnlyList<TransactionSummaryDto>> GetRecentAsync(
        int take = 50,
        CancellationToken cancellationToken = default)
    {
        EnsurePermission(PermissionCatalog.SalesViewHistory);

        using var context = _contextFactory.Create();

        var transactions = await context.Transactions
            .OrderByDescending(row => row.CreatedAt)
            .Take(take)
            .ToListAsync(cancellationToken);

        var userNames = await context.Users
            .ToDictionaryAsync(user => user.Id, user => user.Name, cancellationToken);

        var customerNames = await context.Customers
            .ToDictionaryAsync(customer => customer.Id, customer => customer.Name, cancellationToken);

        return transactions
            .Select(row => new TransactionSummaryDto(
                row.Id,
                row.InvoiceNumber,
                row.CreatedAt,
                userNames.GetValueOrDefault(row.UserId, "-"),
                row.CustomerId is null ? null : customerNames.GetValueOrDefault(row.CustomerId),
                row.TotalNetAmount,
                row.PaymentMethod,
                row.Status))
            .ToList();
    }

    public async Task<TransactionDto> GetDetailAsync(
        string transactionId,
        CancellationToken cancellationToken = default)
    {
        EnsurePermission(PermissionCatalog.SalesViewHistory);

        using var context = _contextFactory.Create();

        var transaction = await context.Transactions
            .FirstOrDefaultAsync(row => row.Id == transactionId, cancellationToken)
            ?? throw new InvalidOperationException("Transaksi tidak ditemukan.");

        return await MapTransactionAsync(context, transaction, cancellationToken);
    }

    public async Task VoidAsync(VoidTransactionRequest request, CancellationToken cancellationToken = default)
    {
        EnsurePermission(PermissionCatalog.SalesVoid);
        await _licenseService.EnsureWriteAllowedAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(request.Reason))
            throw new InvalidOperationException("Alasan pembatalan wajib diisi.");

        using var context = _contextFactory.Create();

        var transaction = await context.Transactions
            .FirstOrDefaultAsync(row => row.Id == request.TransactionId, cancellationToken)
            ?? throw new InvalidOperationException("Transaksi tidak ditemukan.");

        if (transaction.Status != TransactionStatus.COMPLETED)
            throw new InvalidOperationException("Transaksi ini sudah dibatalkan sebelumnya.");

        var items = await context.TransactionItems
            .Where(item => item.TransactionId == transaction.Id)
            .ToListAsync(cancellationToken);

        var now = _clock.UtcNow;
        var userId = RequireUserId();

        foreach (var item in items.Where(row => row.ItemType == TransactionItemType.PRODUCT && row.ProductId is not null))
        {
            var product = await context.Products.FirstOrDefaultAsync(row => row.Id == item.ProductId, cancellationToken);

            if (product is null || !product.TrackStock)
                continue;

            await ApplyVoidStockAsync(context, product, item, userId, transaction.Id, now, cancellationToken);
        }

        transaction.Status = TransactionStatus.VOIDED;
        transaction.VoidedAt = now;
        transaction.VoidedByUserId = userId;
        transaction.VoidReason = request.Reason.Trim();
        transaction.UpdatedAt = now;

        await context.SaveChangesAsync(cancellationToken);

        await _activityLogService.LogAsync(
            "SALE_VOIDED",
            $"Transaksi {transaction.InvoiceNumber} dibatalkan. Alasan: {transaction.VoidReason}.",
            userId: _session.User?.UserId,
            entityType: "transactions",
            entityId: transaction.Id,
            cancellationToken: cancellationToken);
    }

    private async Task<SaleComputation> ComputeAsync(
        IKingraPosDbContext context,
        CheckoutRequest request,
        AppSettings settings,
        CancellationToken cancellationToken)
    {
        if (request.Lines.Count == 0)
            throw new InvalidOperationException("Keranjang masih kosong.");

        var canDiscount = _permissionService.HasPermission(PermissionCatalog.SalesDiscount);
        var canEditPrice = _permissionService.HasPermission(PermissionCatalog.SalesEditPrice);
        var canEditServiceFee = _permissionService.HasPermission(PermissionCatalog.SalesEditServiceFee);
        var canViewCost = _permissionService.HasPermission(PermissionCatalog.ProductViewCostPrice);

        var lines = new List<ComputedLine>();
        var quoteLines = new List<SaleLineDto>();

        long itemsAmount = 0;
        long serviceAmount = 0;
        long totalCostPrice = 0;
        var sortOrder = 0;

        foreach (var line in request.Lines)
        {
            if (line.Quantity <= 0)
                throw new InvalidOperationException("Jumlah setiap item harus lebih dari nol.");

            var product = await context.Products
                .FirstOrDefaultAsync(row => row.Id == line.ProductId && row.DeletedAt == null, cancellationToken)
                ?? throw new InvalidOperationException($"Produk '{line.ProductId}' tidak ditemukan.");

            var productUnit = await ResolveProductUnitAsync(context, product, line.ProductUnitId, cancellationToken);
            var unit = await context.Units
                .FirstOrDefaultAsync(row => row.Id == productUnit.UnitId, cancellationToken);

            EnsureDecimalAllowed(unit, product.Name, line.Quantity);

            var price = await context.ProductPrices
                .FirstOrDefaultAsync(
                    row => row.ProductUnitId == productUnit.Id && row.DeletedAt == null,
                    cancellationToken)
                ?? throw new InvalidOperationException($"Harga jual '{product.Name}' belum diatur.");

            var tiers = await context.ProductPriceTiers
                .Where(row => row.ProductPriceId == price.Id && row.DeletedAt == null)
                .ToListAsync(cancellationToken);

            var tierPrice = TierPriceResolver.Resolve(price.SellingPrice, tiers, line.Quantity);
            var selectedTier = tiers
                .Where(row => row.MinimumQuantity <= line.Quantity)
                .OrderByDescending(row => row.MinimumQuantity)
                .FirstOrDefault();

            var sellingPrice = tierPrice;

            if (line.SellingPriceOverride is not null && line.SellingPriceOverride.Value != tierPrice)
            {
                if (!canEditPrice)
                    throw new UnauthorizedAccessException("Anda tidak memiliki izin mengubah harga jual.");

                sellingPrice = line.SellingPriceOverride.Value;
            }

            if (sellingPrice < 0)
                throw new InvalidOperationException("Harga jual tidak boleh negatif.");

            var lineAmount = SaleTotalsCalculator.LineAmount(sellingPrice, line.Quantity);

            if (line.DiscountAmount < 0)
                throw new InvalidOperationException("Diskon tidak boleh negatif.");

            if (line.DiscountAmount > 0 && !canDiscount)
                throw new UnauthorizedAccessException("Anda tidak memiliki izin memberikan diskon.");

            if (line.DiscountAmount > lineAmount)
                throw new InvalidOperationException($"Diskon '{product.Name}' melebihi nilai baris.");

            var conversionFactor = productUnit.ConversionFactor;
            var quantityBase = ConvertToBaseUnit(line.Quantity, conversionFactor);
            var costPrice = RoundToRupiah((decimal)product.BaseCostPrice * conversionFactor / 1000m);
            var subtotal = lineAmount - line.DiscountAmount;

            var productItem = new TransactionItem
            {
                ItemType = TransactionItemType.PRODUCT,
                ProductId = product.Id,
                ProductUnitId = productUnit.Id,
                Description = product.Name,
                PriceTierId = selectedTier?.Id,
                Quantity = line.Quantity,
                ConversionFactor = conversionFactor,
                QuantityBase = quantityBase,
                CostPrice = costPrice,
                SellingPrice = sellingPrice,
                DiscountAmount = line.DiscountAmount,
                SubtotalPrice = subtotal,
                SortOrder = sortOrder++
            };

            lines.Add(new ComputedLine(productItem, product, false));

            itemsAmount += subtotal;
            totalCostPrice += (long)Math.Round((decimal)costPrice * line.Quantity / 1000m, MidpointRounding.AwayFromZero);

            quoteLines.Add(new SaleLineDto(
                product.Id,
                product.Name,
                productUnit.Id,
                unit?.Name ?? "-",
                line.Quantity,
                conversionFactor,
                quantityBase,
                sellingPrice,
                canViewCost ? costPrice : null,
                line.DiscountAmount,
                subtotal,
                false,
                null));

            if (!line.ApplyServiceFee || !settings.ServiceFeeEnabled)
                continue;

            var defaultFee = product.DefaultServiceFee ?? 0;
            var fee = line.ServiceFee ?? defaultFee;

            if (line.ServiceFee is not null && line.ServiceFee.Value != defaultFee && !canEditServiceFee)
                throw new UnauthorizedAccessException("Anda tidak memiliki izin mengubah biaya jasa.");

            if (fee <= 0)
                continue;

            var serviceItem = new TransactionItem
            {
                ParentItemId = productItem.Id,
                ItemType = TransactionItemType.SERVICE,
                Description = $"Jasa {product.Name}",
                Quantity = BaseUnitConversionFactor,
                ConversionFactor = BaseUnitConversionFactor,
                QuantityBase = 0,
                CostPrice = 0,
                SellingPrice = fee,
                DiscountAmount = 0,
                SubtotalPrice = fee,
                SortOrder = sortOrder++
            };

            lines.Add(new ComputedLine(serviceItem, null, true));
            serviceAmount += fee;

            quoteLines.Add(new SaleLineDto(
                product.Id,
                product.Name,
                null,
                "-",
                BaseUnitConversionFactor,
                BaseUnitConversionFactor,
                0,
                fee,
                null,
                0,
                fee,
                true,
                fee));
        }

        if (request.TransactionDiscountAmount < 0)
            throw new InvalidOperationException("Diskon transaksi tidak boleh negatif.");

        if (request.TransactionDiscountAmount > 0 && !canDiscount)
            throw new UnauthorizedAccessException("Anda tidak memiliki izin memberikan diskon.");

        var gross = itemsAmount + serviceAmount;

        if (request.TransactionDiscountAmount > gross)
            throw new InvalidOperationException("Diskon transaksi melebihi total belanja.");

        var taxRate = settings.TaxEnabled ? settings.TaxRate : 0;

        var totals = SaleTotalsCalculator.Calculate(
            itemsAmount,
            serviceAmount,
            request.TransactionDiscountAmount,
            0,
            taxRate,
            settings.TaxInclusive,
            settings.CashRounding);

        var quote = new SaleQuoteDto(
            quoteLines,
            totals.ItemsAmount,
            totals.ServiceAmount,
            totals.TotalGrossAmount,
            totals.DiscountAmount,
            totals.PointDiscountAmount,
            taxRate,
            settings.TaxName,
            settings.TaxInclusive,
            totals.TaxAmount,
            totals.RoundingAmount,
            totals.TotalNetAmount,
            canDiscount,
            canEditServiceFee);

        return new SaleComputation(lines, quote, totalCostPrice);
    }

    private async Task ApplySaleStockAsync(
        IKingraPosDbContext context,
        Product product,
        TransactionItem item,
        string userId,
        string transactionId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var allowNegativeStock = await AllowsNegativeStockAsync(context, cancellationToken);
        var inventory = await GetOrCreateInventoryAsync(context, product.Id, cancellationToken);

        var before = inventory.StockQuantity;
        var after = before - item.QuantityBase;

        if (!allowNegativeStock && after < 0)
        {
            throw new InvalidOperationException(
                $"Stok '{product.Name}' tidak mencukupi (tersisa {before / 1000m:0.###}).");
        }

        inventory.StockQuantity = after;
        inventory.LastMutationAt = now;
        inventory.UpdatedAt = now;

        context.StockMutations.Add(new StockMutation
        {
            ProductId = product.Id,
            ProductUnitId = item.ProductUnitId,
            UserId = userId,
            MutationType = StockMutationType.SALE,
            StockBucket = StockBucket.AVAILABLE,
            Quantity = -item.QuantityBase,
            StockBefore = before,
            StockAfter = after,
            ReferenceType = StockReferenceType.TRANSACTION,
            ReferenceId = transactionId,
            CreatedAt = now
        });
    }

    private async Task ApplyVoidStockAsync(
        IKingraPosDbContext context,
        Product product,
        TransactionItem item,
        string userId,
        string transactionId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var inventory = await GetOrCreateInventoryAsync(context, product.Id, cancellationToken);

        var before = inventory.StockQuantity;
        var after = before + item.QuantityBase;

        inventory.StockQuantity = after;
        inventory.LastMutationAt = now;
        inventory.UpdatedAt = now;

        context.StockMutations.Add(new StockMutation
        {
            ProductId = product.Id,
            ProductUnitId = item.ProductUnitId,
            UserId = userId,
            MutationType = StockMutationType.SALE_VOID,
            StockBucket = StockBucket.AVAILABLE,
            Quantity = item.QuantityBase,
            StockBefore = before,
            StockAfter = after,
            ReferenceType = StockReferenceType.TRANSACTION,
            ReferenceId = transactionId,
            CreatedAt = now
        });
    }

    private async Task<TransactionDto> MapTransactionAsync(
        IKingraPosDbContext context,
        Transaction transaction,
        CancellationToken cancellationToken)
    {
        var canViewCost = _permissionService.HasPermission(PermissionCatalog.ProductViewCostPrice);

        var items = await context.TransactionItems
            .Where(row => row.TransactionId == transaction.Id)
            .OrderBy(row => row.SortOrder)
            .ToListAsync(cancellationToken);

        var productIds = items
            .Where(row => row.ProductId is not null)
            .Select(row => row.ProductId!)
            .Distinct()
            .ToList();

        var productNames = await context.Products
            .Where(row => productIds.Contains(row.Id))
            .ToDictionaryAsync(row => row.Id, row => row.Name, cancellationToken);

        var unitIds = items
            .Where(row => row.ProductUnitId is not null)
            .Select(row => row.ProductUnitId!)
            .Distinct()
            .ToList();

        var unitNames = await context.ProductUnits
            .Where(row => unitIds.Contains(row.Id))
            .Join(context.Units, unit => unit.UnitId, master => master.Id, (unit, master) => new
            {
                unit.Id,
                master.Name
            })
            .ToDictionaryAsync(row => row.Id, row => row.Name, cancellationToken);

        var userName = await context.Users
            .Where(row => row.Id == transaction.UserId)
            .Select(row => row.Name)
            .FirstOrDefaultAsync(cancellationToken) ?? "-";

        var customerName = transaction.CustomerId is null
            ? null
            : await context.Customers
                .Where(row => row.Id == transaction.CustomerId)
                .Select(row => row.Name)
                .FirstOrDefaultAsync(cancellationToken);

        var voidedByName = transaction.VoidedByUserId is null
            ? null
            : await context.Users
                .Where(row => row.Id == transaction.VoidedByUserId)
                .Select(row => row.Name)
                .FirstOrDefaultAsync(cancellationToken);

        var settings = await LoadSettingsAsync(context, cancellationToken);

        var itemDtos = items
            .Select(row => new TransactionItemDto(
                row.Id,
                row.ItemType,
                row.ProductId,
                row.ProductId is null ? null : productNames.GetValueOrDefault(row.ProductId),
                row.ProductUnitId is null ? null : unitNames.GetValueOrDefault(row.ProductUnitId),
                row.Description,
                row.Quantity,
                row.ConversionFactor,
                row.QuantityBase,
                canViewCost ? row.CostPrice : null,
                row.SellingPrice,
                row.DiscountAmount,
                row.SubtotalPrice,
                row.SortOrder))
            .ToList();

        return new TransactionDto(
            transaction.Id,
            transaction.InvoiceNumber,
            userName,
            customerName,
            transaction.ShiftId,
            transaction.CreatedAt,
            transaction.ItemsAmount,
            transaction.ServiceAmount,
            transaction.TotalGrossAmount,
            transaction.DiscountAmount,
            transaction.PointDiscountAmount,
            transaction.TaxRate,
            settings.TaxName,
            transaction.TaxInclusive,
            transaction.TaxAmount,
            transaction.RoundingAmount,
            transaction.TotalNetAmount,
            canViewCost ? transaction.TotalCostPrice : null,
            transaction.PaymentMethod,
            transaction.PaymentProvider,
            transaction.PaymentReference,
            transaction.AmountPaid,
            transaction.AmountChange,
            transaction.PointsEarned,
            transaction.PointsUsed,
            transaction.Status,
            transaction.VoidedAt,
            voidedByName,
            transaction.VoidReason,
            transaction.Notes,
            itemDtos);
    }

    private static async Task<AppSettings> LoadSettingsAsync(
        IKingraPosDbContext context,
        CancellationToken cancellationToken)
    {
        var settings = await context.Settings
            .FirstOrDefaultAsync(row => row.Id == "main", cancellationToken);

        return settings ?? new AppSettings();
    }

    private static async Task<ProductUnit> ResolveProductUnitAsync(
        IKingraPosDbContext context,
        Product product,
        string? productUnitId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(productUnitId))
        {
            return await context.ProductUnits.FirstOrDefaultAsync(
                unit => unit.ProductId == product.Id && unit.IsBaseUnit && unit.DeletedAt == null,
                cancellationToken)
                ?? throw new InvalidOperationException($"Satuan dasar '{product.Name}' tidak ditemukan.");
        }

        return await context.ProductUnits.FirstOrDefaultAsync(
            unit => unit.Id == productUnitId && unit.ProductId == product.Id && unit.DeletedAt == null,
            cancellationToken)
            ?? throw new InvalidOperationException($"Satuan jual tersebut bukan milik produk '{product.Name}'.");
    }

    private static void EnsureDecimalAllowed(Unit? unit, string productName, long quantity)
    {
        if (unit is null || unit.AllowDecimal)
            return;

        if (quantity % BaseUnitConversionFactor != 0)
        {
            throw new InvalidOperationException(
                $"Satuan '{unit.Name}' untuk '{productName}' tidak mendukung jumlah desimal.");
        }
    }

    private static async Task<string?> ResolveCustomerIdAsync(
        IKingraPosDbContext context,
        string? customerId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(customerId))
            return null;

        var exists = await context.Customers
            .AnyAsync(row => row.Id == customerId && row.DeletedAt == null, cancellationToken);

        if (!exists)
            throw new InvalidOperationException("Pelanggan tidak ditemukan.");

        return customerId;
    }

    private async Task<string> GenerateInvoiceNumberAsync(
        IKingraPosDbContext context,
        AppSettings settings,
        CancellationToken cancellationToken)
    {
        var local = _clock.UtcNow.ToOffset(TimeSpan.FromMinutes(settings.UtcOffsetMinutes));
        var prefix = $"{settings.InvoicePrefix}-{local:yyyyMMdd}-";

        var sequence = await context.Transactions
            .CountAsync(row => row.InvoiceNumber.StartsWith(prefix), cancellationToken) + 1;

        return $"{prefix}{sequence:D4}";
    }

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

    private static long ConvertToBaseUnit(long quantity, long conversionFactor) =>
        (long)Math.Round((decimal)quantity * conversionFactor / 1000m, MidpointRounding.AwayFromZero);

    private static long RoundToRupiah(decimal amount) =>
        (long)Math.Round(amount, MidpointRounding.AwayFromZero);

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private string RequireUserId() =>
        _session.User?.UserId ?? throw new InvalidOperationException("Sesi pengguna tidak ditemukan.");

    private void EnsurePermission(string permissionKey)
    {
        if (!_permissionService.HasPermission(permissionKey))
            throw new UnauthorizedAccessException($"Anda tidak memiliki izin '{permissionKey}'.");
    }

    private sealed record ComputedLine(TransactionItem Item, Product? Product, bool IsService);

    private sealed record SaleComputation(
        IReadOnlyList<ComputedLine> Lines,
        SaleQuoteDto Quote,
        long TotalCostPrice);
}
