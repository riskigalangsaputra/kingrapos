using KingraPOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace KingraPOS.Infrastructure.Persistence;

public partial class KingraPosDbContext
{
    private static void ConfigureRelationships(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>()
            .HasOne<Role>().WithMany()
            .HasForeignKey(entity => entity.RoleId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<RolePermission>()
            .HasOne<Role>().WithMany()
            .HasForeignKey(entity => entity.RoleId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<RolePermission>()
            .HasOne<Permission>().WithMany()
            .HasForeignKey(entity => entity.PermissionKey)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<UserPermission>()
            .HasOne<User>().WithMany()
            .HasForeignKey(entity => entity.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<UserPermission>()
            .HasOne<Permission>().WithMany()
            .HasForeignKey(entity => entity.PermissionKey)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<UserPermission>()
            .HasOne<User>().WithMany()
            .HasForeignKey(entity => entity.GrantedByUserId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Product>()
            .HasOne<Category>().WithMany()
            .HasForeignKey(entity => entity.CategoryId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Product>()
            .HasOne<Unit>().WithMany()
            .HasForeignKey(entity => entity.BaseUnitId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ProductUnit>()
            .HasOne<Product>().WithMany()
            .HasForeignKey(entity => entity.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ProductUnit>()
            .HasOne<Unit>().WithMany()
            .HasForeignKey(entity => entity.UnitId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ProductPrice>()
            .HasOne<ProductUnit>().WithOne()
            .HasForeignKey<ProductPrice>(entity => entity.ProductUnitId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ProductPriceTier>()
            .HasOne<ProductPrice>().WithMany()
            .HasForeignKey(entity => entity.ProductPriceId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<SupplierContact>()
            .HasOne<Supplier>().WithMany()
            .HasForeignKey(entity => entity.SupplierId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<LoyaltyRewardItem>()
            .HasOne<Product>().WithMany()
            .HasForeignKey(entity => entity.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<LoyaltyRewardItem>()
            .HasOne<ProductUnit>().WithMany()
            .HasForeignKey(entity => entity.ProductUnitId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Inventory>()
            .HasOne<Product>().WithOne()
            .HasForeignKey<Inventory>(entity => entity.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<StockAdjustment>()
            .HasOne<User>().WithMany()
            .HasForeignKey(entity => entity.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<StockAdjustment>()
            .HasOne<Supplier>().WithMany()
            .HasForeignKey(entity => entity.SupplierId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<StockAdjustment>()
            .HasOne<SupplierContact>().WithMany()
            .HasForeignKey(entity => entity.SupplierContactId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<StockAdjustment>()
            .HasOne<User>().WithMany()
            .HasForeignKey(entity => entity.ReceivedByUserId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<StockAdjustmentItem>()
            .HasOne<StockAdjustment>().WithMany()
            .HasForeignKey(entity => entity.StockAdjustmentId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<StockAdjustmentItem>()
            .HasOne<Product>().WithMany()
            .HasForeignKey(entity => entity.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<StockAdjustmentItem>()
            .HasOne<ProductUnit>().WithMany()
            .HasForeignKey(entity => entity.ProductUnitId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<ProductRejectLog>()
            .HasOne<Product>().WithMany()
            .HasForeignKey(entity => entity.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ProductRejectLog>()
            .HasOne<User>().WithMany()
            .HasForeignKey(entity => entity.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ProductRejectLog>()
            .HasOne<Supplier>().WithMany()
            .HasForeignKey(entity => entity.SupplierId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<ProductRejectLog>()
            .HasOne<User>().WithMany()
            .HasForeignKey(entity => entity.ResolvedByUserId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<StockMutation>()
            .HasOne<Product>().WithMany()
            .HasForeignKey(entity => entity.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<StockMutation>()
            .HasOne<ProductUnit>().WithMany()
            .HasForeignKey(entity => entity.ProductUnitId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<StockMutation>()
            .HasOne<Supplier>().WithMany()
            .HasForeignKey(entity => entity.SupplierId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<StockMutation>()
            .HasOne<User>().WithMany()
            .HasForeignKey(entity => entity.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<CashierShift>()
            .HasOne<User>().WithMany()
            .HasForeignKey(entity => entity.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Transaction>()
            .HasOne<User>().WithMany()
            .HasForeignKey(entity => entity.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Transaction>()
            .HasOne<Customer>().WithMany()
            .HasForeignKey(entity => entity.CustomerId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Transaction>()
            .HasOne<CashierShift>().WithMany()
            .HasForeignKey(entity => entity.ShiftId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Transaction>()
            .HasOne<User>().WithMany()
            .HasForeignKey(entity => entity.VoidedByUserId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<TransactionItem>()
            .HasOne<Transaction>().WithMany()
            .HasForeignKey(entity => entity.TransactionId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<TransactionItem>()
            .HasOne<TransactionItem>().WithMany()
            .HasForeignKey(entity => entity.ParentItemId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<TransactionItem>()
            .HasOne<Product>().WithMany()
            .HasForeignKey(entity => entity.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<TransactionItem>()
            .HasOne<ProductUnit>().WithMany()
            .HasForeignKey(entity => entity.ProductUnitId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<TransactionItem>()
            .HasOne<ProductPriceTier>().WithMany()
            .HasForeignKey(entity => entity.PriceTierId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<CustomerPointRedemption>()
            .HasOne<Customer>().WithMany()
            .HasForeignKey(entity => entity.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<CustomerPointRedemption>()
            .HasOne<User>().WithMany()
            .HasForeignKey(entity => entity.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<CustomerPointRedemption>()
            .HasOne<Transaction>().WithMany()
            .HasForeignKey(entity => entity.TransactionId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<CustomerPointRedemption>()
            .HasOne<LoyaltyRewardItem>().WithMany()
            .HasForeignKey(entity => entity.RewardItemId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<CustomerPointLedger>()
            .HasOne<Customer>().WithMany()
            .HasForeignKey(entity => entity.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<CustomerPointLedger>()
            .HasOne<User>().WithMany()
            .HasForeignKey(entity => entity.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<CustomerPointLedger>()
            .HasOne<Transaction>().WithMany()
            .HasForeignKey(entity => entity.TransactionId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<CustomerPointLedger>()
            .HasOne<CustomerPointRedemption>().WithMany()
            .HasForeignKey(entity => entity.RedemptionId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Expense>()
            .HasOne<ExpenseCategory>().WithMany()
            .HasForeignKey(entity => entity.ExpenseCategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Expense>()
            .HasOne<User>().WithMany()
            .HasForeignKey(entity => entity.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Expense>()
            .HasOne<CashierShift>().WithMany()
            .HasForeignKey(entity => entity.ShiftId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Expense>()
            .HasOne<Supplier>().WithMany()
            .HasForeignKey(entity => entity.SupplierId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Expense>()
            .HasOne<User>().WithMany()
            .HasForeignKey(entity => entity.EmployeeUserId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<ProductPriceChangelog>()
            .HasOne<Product>().WithMany()
            .HasForeignKey(entity => entity.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ProductPriceChangelog>()
            .HasOne<ProductUnit>().WithMany()
            .HasForeignKey(entity => entity.ProductUnitId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ProductPriceChangelog>()
            .HasOne<User>().WithMany()
            .HasForeignKey(entity => entity.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ActivityLog>()
            .HasOne<User>().WithMany()
            .HasForeignKey(entity => entity.UserId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<BackupLog>()
            .HasOne<BackupSchedule>().WithMany()
            .HasForeignKey(entity => entity.ScheduleId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<BackupLog>()
            .HasOne<User>().WithMany()
            .HasForeignKey(entity => entity.TriggeredByUserId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<RestoreLog>()
            .HasOne<User>().WithMany()
            .HasForeignKey(entity => entity.UserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
