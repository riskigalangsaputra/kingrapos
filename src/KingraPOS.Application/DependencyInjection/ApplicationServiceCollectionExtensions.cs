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

        services.AddTransient<IValidator<SetupRequest>, SetupRequestValidator>();
        services.AddTransient<IValidator<LoginRequest>, LoginRequestValidator>();
        services.AddTransient<IValidator<CreateUserRequest>, CreateUserRequestValidator>();

        return services;
    }
}
