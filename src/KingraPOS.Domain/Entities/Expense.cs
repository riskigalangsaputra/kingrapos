using KingraPOS.Domain.Common;
using KingraPOS.Domain.Enums;

namespace KingraPOS.Domain.Entities;

public class Expense : Entity
{
    public string ExpenseCategoryId { get; set; } = string.Empty;

    public string UserId { get; set; } = string.Empty;

    public string? ShiftId { get; set; }

    public string? SupplierId { get; set; }

    public string? EmployeeUserId { get; set; }

    public long Amount { get; set; }

    public DateOnly ExpenseDate { get; set; }

    public PaymentSource PaymentSource { get; set; }

    public string? ReceiptImagePath { get; set; }

    public string? Notes { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }
}
