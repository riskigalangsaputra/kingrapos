namespace KingraPOS.Application.Security;

public static class PermissionCatalog
{
    public const string OwnerRoleName = "Owner";
    public const string AdminRoleName = "Admin";
    public const string CashierRoleName = "Kasir";

    public const string SalesCreate = "sales.create";
    public const string SalesEditPrice = "sales.edit_price";
    public const string SalesEditServiceFee = "sales.edit_service_fee";
    public const string SalesDiscount = "sales.discount";
    public const string SalesVoid = "sales.void";
    public const string SalesViewHistory = "sales.view_history";
    public const string ShiftOpen = "shift.open";
    public const string ShiftClose = "shift.close";
    public const string ShiftViewAll = "shift.view_all";
    public const string ProductView = "product.view";
    public const string ProductCreate = "product.create";
    public const string ProductUpdate = "product.update";
    public const string ProductDelete = "product.delete";
    public const string ProductManagePrice = "product.manage_price";
    public const string ProductViewCostPrice = "product.view_cost_price";
    public const string CategoryManage = "category.manage";
    public const string StockView = "stock.view";
    public const string StockUpdate = "stock.update";
    public const string StockReceive = "stock.receive";
    public const string StockViewHistory = "stock.view_history";
    public const string StockReject = "stock.reject";
    public const string SupplierView = "supplier.view";
    public const string SupplierManage = "supplier.manage";
    public const string CustomerView = "customer.view";
    public const string CustomerManage = "customer.manage";
    public const string LoyaltyInputPoints = "loyalty.input_points";
    public const string LoyaltyRedeem = "loyalty.redeem";
    public const string LoyaltySettings = "loyalty.settings";
    public const string ExpenseView = "expense.view";
    public const string ExpenseCreate = "expense.create";
    public const string ExpenseUpdate = "expense.update";
    public const string ExpenseDelete = "expense.delete";
    public const string ReportSales = "report.sales";
    public const string ReportProfitLoss = "report.profit_loss";
    public const string ReportTopProducts = "report.top_products";
    public const string ReportStock = "report.stock";
    public const string ReportRejects = "report.rejects";
    public const string ReportExpenses = "report.expenses";
    public const string ReportEmployees = "report.employees";
    public const string UserView = "user.view";
    public const string UserManage = "user.manage";
    public const string RoleManage = "role.manage";
    public const string SettingsTax = "settings.tax";
    public const string SettingsServiceFee = "settings.service_fee";
    public const string SettingsPrinter = "settings.printer";
    public const string SettingsBackup = "settings.backup";
    public const string SettingsRestore = "settings.restore";
    public const string SettingsLicense = "settings.license";
    public const string AuditView = "audit.view";

    public static IReadOnlyList<string> All { get; } = new[]
    {
        SalesCreate, SalesEditPrice, SalesEditServiceFee, SalesDiscount, SalesVoid, SalesViewHistory,
        ShiftOpen, ShiftClose, ShiftViewAll,
        ProductView, ProductCreate, ProductUpdate, ProductDelete, ProductManagePrice, ProductViewCostPrice, CategoryManage,
        StockView, StockUpdate, StockReceive, StockViewHistory, StockReject,
        SupplierView, SupplierManage,
        CustomerView, CustomerManage,
        LoyaltyInputPoints, LoyaltyRedeem, LoyaltySettings,
        ExpenseView, ExpenseCreate, ExpenseUpdate, ExpenseDelete,
        ReportSales, ReportProfitLoss, ReportTopProducts, ReportStock, ReportRejects, ReportExpenses, ReportEmployees,
        UserView, UserManage, RoleManage,
        SettingsTax, SettingsServiceFee, SettingsPrinter, SettingsBackup, SettingsRestore, SettingsLicense, AuditView
    };

    private static readonly string[] AdminExcluded =
    {
        SettingsLicense,
        SettingsRestore,
        RoleManage
    };

    private static readonly string[] CashierPermissions =
    {
        SalesCreate,
        SalesViewHistory,
        ShiftOpen,
        ShiftClose,
        ProductView,
        StockView,
        StockUpdate,
        CustomerView,
        CustomerManage,
        LoyaltyInputPoints,
        ReportSales
    };

    public static IReadOnlyCollection<string> GetDefaultPermissions(string roleName) => roleName switch
    {
        OwnerRoleName => All,
        AdminRoleName => All.Where(permission => !AdminExcluded.Contains(permission)).ToArray(),
        CashierRoleName => CashierPermissions,
        _ => Array.Empty<string>()
    };
}
