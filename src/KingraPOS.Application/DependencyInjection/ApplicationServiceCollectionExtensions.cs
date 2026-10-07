using FluentValidation;
using KingraPOS.Application.Abstractions.Security;
using KingraPOS.Application.Abstractions.Services;
using KingraPOS.Application.Dtos;
using KingraPOS.Application.Security;
using KingraPOS.Application.Services;
using KingraPOS.Application.Validation;
using Microsoft.Extensions.DependencyInjection;

namespace KingraPOS.Application.DependencyInjection;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<ICurrentUserSession, CurrentUserSession>();
        services.AddSingleton<IPermissionService, PermissionService>();
        services.AddSingleton<IActivityLogService, ActivityLogService>();
        services.AddSingleton<ISetupService, SetupService>();
        services.AddSingleton<IAuthenticationService, AuthenticationService>();
        services.AddSingleton<IUserManagementService, UserManagementService>();
        services.AddSingleton<ILicenseService, LicenseService>();
        services.AddSingleton<IRoleManagementService, RoleManagementService>();
        services.AddSingleton<IBackupService, BackupService>();
        services.AddSingleton<IRestoreService, RestoreService>();
        services.AddSingleton<ICategoryService, CategoryService>();
        services.AddSingleton<IUnitService, UnitService>();
        services.AddSingleton<ISupplierService, SupplierService>();
        services.AddSingleton<IProductService, ProductService>();
        services.AddSingleton<IStockService, StockService>();
        services.AddSingleton<IShiftService, ShiftService>();
        services.AddSingleton<ITransactionService, TransactionService>();

        services.AddTransient<IValidator<SetupRequest>, SetupRequestValidator>();
        services.AddTransient<IValidator<LoginRequest>, LoginRequestValidator>();
        services.AddTransient<IValidator<CreateUserRequest>, CreateUserRequestValidator>();
        services.AddTransient<IValidator<LicenseActivationRequest>, LicenseActivationRequestValidator>();
        services.AddTransient<IValidator<BackupScheduleRequest>, BackupScheduleRequestValidator>();
        services.AddTransient<IValidator<CategoryRequest>, CategoryRequestValidator>();
        services.AddTransient<IValidator<UnitRequest>, UnitRequestValidator>();
        services.AddTransient<IValidator<SupplierRequest>, SupplierRequestValidator>();
        services.AddTransient<IValidator<SupplierContactRequest>, SupplierContactRequestValidator>();
        services.AddTransient<IValidator<CreateProductRequest>, CreateProductRequestValidator>();
        services.AddTransient<IValidator<UpdateProductRequest>, UpdateProductRequestValidator>();
        services.AddTransient<IValidator<ProductUnitRequest>, ProductUnitRequestValidator>();
        services.AddTransient<IValidator<IReadOnlyCollection<ProductPriceTierRequest>>, ProductPriceTierListValidator>();
        services.AddTransient<IValidator<StockAdjustmentRequest>, StockAdjustmentRequestValidator>();
        services.AddTransient<IValidator<OpenShiftRequest>, OpenShiftRequestValidator>();
        services.AddTransient<IValidator<CloseShiftRequest>, CloseShiftRequestValidator>();
        services.AddTransient<IValidator<SwitchShiftRequest>, SwitchShiftRequestValidator>();
        services.AddTransient<IValidator<CheckoutRequest>, CheckoutRequestValidator>();
        services.AddTransient<IValidator<VoidTransactionRequest>, VoidTransactionRequestValidator>();

        return services;
    }
}
