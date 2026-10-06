using KingraPOS.Application.Abstractions.Persistence;
using KingraPOS.Domain.Entities;
using KingraPOS.Domain.Enums;
using KingraPOS.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;

namespace KingraPOS.Infrastructure.Persistence;

public partial class KingraPosDbContext : DbContext, IKingraPosDbContext
{
    private const string SqliteNow = "strftime('%Y-%m-%dT%H:%M:%fZ', 'now')";

    public KingraPosDbContext(DbContextOptions<KingraPosDbContext> options)
        : base(options)
    {
    }

    public DbSet<AppMeta> AppMetaEntries => Set<AppMeta>();

    public DbSet<BusinessProfile> BusinessProfiles => Set<BusinessProfile>();

    public DbSet<AppSettings> Settings => Set<AppSettings>();

    public DbSet<AppLicense> AppLicenses => Set<AppLicense>();

    public DbSet<Permission> Permissions => Set<Permission>();

    public DbSet<Role> Roles => Set<Role>();

    public DbSet<User> Users => Set<User>();

    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();

    public DbSet<UserPermission> UserPermissions => Set<UserPermission>();

    public DbSet<Category> Categories => Set<Category>();

    public DbSet<Unit> Units => Set<Unit>();

    public DbSet<Product> Products => Set<Product>();

    public DbSet<ProductUnit> ProductUnits => Set<ProductUnit>();

    public DbSet<ProductPrice> ProductPrices => Set<ProductPrice>();

    public DbSet<ProductPriceTier> ProductPriceTiers => Set<ProductPriceTier>();

    public DbSet<Supplier> Suppliers => Set<Supplier>();

    public DbSet<SupplierContact> SupplierContacts => Set<SupplierContact>();

    public DbSet<ExpenseCategory> ExpenseCategories => Set<ExpenseCategory>();

    public DbSet<Customer> Customers => Set<Customer>();

    public DbSet<LoyaltyPointRule> LoyaltyPointRules => Set<LoyaltyPointRule>();

    public DbSet<LoyaltyRewardItem> LoyaltyRewardItems => Set<LoyaltyRewardItem>();

    public DbSet<Inventory> Inventories => Set<Inventory>();

    public DbSet<StockAdjustment> StockAdjustments => Set<StockAdjustment>();

    public DbSet<StockAdjustmentItem> StockAdjustmentItems => Set<StockAdjustmentItem>();

    public DbSet<ProductRejectLog> ProductRejectLogs => Set<ProductRejectLog>();

    public DbSet<StockMutation> StockMutations => Set<StockMutation>();

    public DbSet<CashierShift> CashierShifts => Set<CashierShift>();

    public DbSet<Transaction> Transactions => Set<Transaction>();

    public DbSet<TransactionItem> TransactionItems => Set<TransactionItem>();

    public DbSet<CustomerPointRedemption> CustomerPointRedemptions => Set<CustomerPointRedemption>();

    public DbSet<CustomerPointLedger> CustomerPointLedgers => Set<CustomerPointLedger>();

    public DbSet<Expense> Expenses => Set<Expense>();

    public DbSet<ProductPriceChangelog> ProductPriceChangelogs => Set<ProductPriceChangelog>();

    public DbSet<ActivityLog> ActivityLogs => Set<ActivityLog>();

    public DbSet<PrinterSetting> PrinterSettings => Set<PrinterSetting>();

    public DbSet<BackupSchedule> BackupSchedules => Set<BackupSchedule>();

    public DbSet<BackupLog> BackupLogs => Set<BackupLog>();

    public DbSet<RestoreLog> RestoreLogs => Set<RestoreLog>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTimeOffset>().HaveConversion<UtcIso8601DateTimeOffsetConverter>();

        configurationBuilder.Properties<LicenseStatus>().HaveConversion<string>();
        configurationBuilder.Properties<PriceTrend>().HaveConversion<string>();
        configurationBuilder.Properties<StockAdjustmentReason>().HaveConversion<string>();
        configurationBuilder.Properties<RejectStatus>().HaveConversion<string>();
        configurationBuilder.Properties<StockMutationType>().HaveConversion<string>();
        configurationBuilder.Properties<StockBucket>().HaveConversion<string>();
        configurationBuilder.Properties<StockReferenceType>().HaveConversion<string>();
        configurationBuilder.Properties<ShiftStatus>().HaveConversion<string>();
        configurationBuilder.Properties<TransactionStatus>().HaveConversion<string>();
        configurationBuilder.Properties<TransactionItemType>().HaveConversion<string>();
        configurationBuilder.Properties<RedemptionType>().HaveConversion<string>();
        configurationBuilder.Properties<PointLedgerEntryType>().HaveConversion<string>();
        configurationBuilder.Properties<PaymentSource>().HaveConversion<string>();
        configurationBuilder.Properties<PriceChangeType>().HaveConversion<string>();
        configurationBuilder.Properties<PrinterType>().HaveConversion<string>();
        configurationBuilder.Properties<PrinterConnectionType>().HaveConversion<string>();
        configurationBuilder.Properties<BackupFrequency>().HaveConversion<string>();
        configurationBuilder.Properties<BackupTriggerType>().HaveConversion<string>();
        configurationBuilder.Properties<JobStatus>().HaveConversion<string>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AppMeta>().ToTable("app_meta");
        modelBuilder.Entity<AppMeta>().HasKey(entity => entity.Key);

        modelBuilder.Entity<BusinessProfile>().ToTable("business_profile");
        modelBuilder.Entity<AppSettings>().ToTable("app_settings");
        modelBuilder.Entity<AppLicense>().ToTable("app_license");

        modelBuilder.Entity<Permission>().ToTable("permissions");
        modelBuilder.Entity<Permission>().HasKey(entity => entity.PermissionKey);

        modelBuilder.Entity<Role>().ToTable("roles");
        modelBuilder.Entity<User>().ToTable("users");

        modelBuilder.Entity<RolePermission>().ToTable("role_permissions");
        modelBuilder.Entity<RolePermission>().HasKey(entity => new { entity.RoleId, entity.PermissionKey });

        modelBuilder.Entity<UserPermission>().ToTable("user_permissions");
        modelBuilder.Entity<UserPermission>().HasKey(entity => new { entity.UserId, entity.PermissionKey });

        modelBuilder.Entity<Category>().ToTable("categories");
        modelBuilder.Entity<Unit>().ToTable("units");
        modelBuilder.Entity<Product>().ToTable("products");
        modelBuilder.Entity<ProductUnit>().ToTable("product_units");
        modelBuilder.Entity<ProductPrice>().ToTable("product_prices");
        modelBuilder.Entity<ProductPriceTier>().ToTable("product_price_tiers");
        modelBuilder.Entity<Supplier>().ToTable("suppliers");
        modelBuilder.Entity<SupplierContact>().ToTable("supplier_contacts");
        modelBuilder.Entity<ExpenseCategory>().ToTable("expense_categories");
        modelBuilder.Entity<Customer>().ToTable("customers");
        modelBuilder.Entity<LoyaltyPointRule>().ToTable("loyalty_point_rules");
        modelBuilder.Entity<LoyaltyRewardItem>().ToTable("loyalty_reward_items");

        modelBuilder.Entity<Inventory>().ToTable("inventories");
        modelBuilder.Entity<StockAdjustment>().ToTable("stock_adjustments");
        modelBuilder.Entity<StockAdjustmentItem>().ToTable("stock_adjustment_items");
        modelBuilder.Entity<ProductRejectLog>().ToTable("product_reject_logs");
        modelBuilder.Entity<StockMutation>().ToTable("stock_mutations");

        modelBuilder.Entity<CashierShift>().ToTable("cashier_shifts");
        modelBuilder.Entity<Transaction>().ToTable("transactions");
        modelBuilder.Entity<TransactionItem>().ToTable("transaction_items");
        modelBuilder.Entity<CustomerPointRedemption>().ToTable("customer_point_redemptions");
        modelBuilder.Entity<CustomerPointLedger>().ToTable("customer_point_ledger");
        modelBuilder.Entity<Expense>().ToTable("expenses");

        modelBuilder.Entity<ProductPriceChangelog>().ToTable("product_price_changelogs");
        modelBuilder.Entity<ActivityLog>().ToTable("activity_logs");
        modelBuilder.Entity<PrinterSetting>().ToTable("printer_settings");
        modelBuilder.Entity<BackupSchedule>().ToTable("backup_schedules");
        modelBuilder.Entity<BackupLog>().ToTable("backup_logs");
        modelBuilder.Entity<RestoreLog>().ToTable("restore_logs");

        ApplySqliteColumnConventions(modelBuilder);
        ConfigureRelationships(modelBuilder);
    }

    private static void ApplySqliteColumnConventions(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                property.SetColumnName(ToSnakeCase(property.Name));

                if (property.ClrType == typeof(DateTimeOffset)
                    && property.Name is "CreatedAt" or "UpdatedAt")
                {
                    property.SetDefaultValueSql(SqliteNow);
                }
            }
        }
    }

    private static string ToSnakeCase(string name)
    {
        var builder = new System.Text.StringBuilder(name.Length + 8);

        for (var index = 0; index < name.Length; index++)
        {
            var character = name[index];

            if (char.IsUpper(character))
            {
                if (index > 0)
                    builder.Append('_');

                builder.Append(char.ToLowerInvariant(character));
            }
            else
            {
                builder.Append(character);
            }
        }

        return builder.ToString();
    }
}
