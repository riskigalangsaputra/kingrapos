using KingraPOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace KingraPOS.Application.Abstractions.Persistence;

public interface IKingraPosDbContext : IDisposable
{
    DbSet<AppMeta> AppMetaEntries { get; }

    DbSet<BusinessProfile> BusinessProfiles { get; }

    DbSet<AppSettings> Settings { get; }

    DbSet<AppLicense> AppLicenses { get; }

    DbSet<Permission> Permissions { get; }

    DbSet<Role> Roles { get; }

    DbSet<User> Users { get; }

    DbSet<RolePermission> RolePermissions { get; }

    DbSet<UserPermission> UserPermissions { get; }

    DbSet<Category> Categories { get; }

    DbSet<Unit> Units { get; }

    DbSet<Product> Products { get; }

    DbSet<ProductUnit> ProductUnits { get; }

    DbSet<ProductPrice> ProductPrices { get; }

    DbSet<ProductPriceTier> ProductPriceTiers { get; }

    DbSet<Supplier> Suppliers { get; }

    DbSet<SupplierContact> SupplierContacts { get; }

    DbSet<ExpenseCategory> ExpenseCategories { get; }

    DbSet<Customer> Customers { get; }

    DbSet<LoyaltyPointRule> LoyaltyPointRules { get; }

    DbSet<LoyaltyRewardItem> LoyaltyRewardItems { get; }

    DbSet<Inventory> Inventories { get; }

    DbSet<StockAdjustment> StockAdjustments { get; }

    DbSet<StockAdjustmentItem> StockAdjustmentItems { get; }

    DbSet<ProductRejectLog> ProductRejectLogs { get; }

    DbSet<StockMutation> StockMutations { get; }

    DbSet<CashierShift> CashierShifts { get; }

    DbSet<Transaction> Transactions { get; }

    DbSet<TransactionItem> TransactionItems { get; }

    DbSet<CustomerPointRedemption> CustomerPointRedemptions { get; }

    DbSet<CustomerPointLedger> CustomerPointLedgers { get; }

    DbSet<Expense> Expenses { get; }

    DbSet<ProductPriceChangelog> ProductPriceChangelogs { get; }

    DbSet<ActivityLog> ActivityLogs { get; }

    DbSet<PrinterSetting> PrinterSettings { get; }

    DbSet<BackupSchedule> BackupSchedules { get; }

    DbSet<BackupLog> BackupLogs { get; }

    DbSet<RestoreLog> RestoreLogs { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
