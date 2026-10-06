using KingraPOS.Application.Abstractions.Persistence;
using KingraPOS.Application.Abstractions.Security;
using KingraPOS.Application.Abstractions.Services;
using KingraPOS.Application.Dtos;
using KingraPOS.Application.Security;
using KingraPOS.Domain.Entities;
using KingraPOS.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace KingraPOS.Application.Services;

public sealed class SetupService : ISetupService
{
    private const string MainRowId = "main";

    private readonly IKingraPosDbContextFactory _contextFactory;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IActivityLogService _activityLogService;
    private readonly IClock _clock;

    public SetupService(
        IKingraPosDbContextFactory contextFactory,
        IPasswordHasher passwordHasher,
        IActivityLogService activityLogService,
        IClock clock)
    {
        _contextFactory = contextFactory;
        _passwordHasher = passwordHasher;
        _activityLogService = activityLogService;
        _clock = clock;
    }

    public async Task<bool> IsCompletedAsync(CancellationToken cancellationToken = default)
    {
        using var context = _contextFactory.Create();
        return await context.BusinessProfiles.AnyAsync(cancellationToken);
    }

    public async Task<SetupStateDto> GetStateAsync(CancellationToken cancellationToken = default)
    {
        using var context = _contextFactory.Create();

        var profile = await context.BusinessProfiles
            .FirstOrDefaultAsync(row => row.Id == MainRowId, cancellationToken);

        var settings = await context.Settings
            .FirstOrDefaultAsync(row => row.Id == MainRowId, cancellationToken);

        return new SetupStateDto(
            profile is not null,
            profile is null
                ? null
                : new BusinessProfileDto(
                    profile.Name,
                    profile.OwnerName,
                    profile.PhoneNumber,
                    profile.Address,
                    profile.TaxNumber,
                    profile.ReceiptFooter),
            new AppSettingsDto(
                settings?.TaxEnabled ?? false,
                settings?.TaxName ?? "PPN",
                settings?.TaxRate ?? 11.0,
                settings?.TaxInclusive ?? false,
                settings?.ServiceFeeEnabled ?? false,
                settings?.AllowNegativeStock ?? false,
                settings?.CashRounding ?? 0,
                settings?.AutoPrintReceipt ?? true,
                settings?.NewProductBadgeDays ?? 7,
                settings?.PriceTrendBadgeDays ?? 7,
                settings?.InvoicePrefix ?? "INV",
                settings?.Timezone ?? "Asia/Jakarta",
                settings?.UtcOffsetMinutes ?? 420));
    }

    public async Task CompleteAsync(SetupRequest request, CancellationToken cancellationToken = default)
    {
        using var context = _contextFactory.Create();

        if (await context.BusinessProfiles.AnyAsync(cancellationToken))
            throw new InvalidOperationException("Setup awal sudah pernah dilakukan.");

        var now = _clock.UtcNow;

        context.BusinessProfiles.Add(new BusinessProfile
        {
            Id = MainRowId,
            Name = request.BusinessName.Trim(),
            OwnerName = request.OwnerName.Trim(),
            PhoneNumber = request.PhoneNumber,
            Address = request.Address,
            TaxNumber = request.TaxNumber,
            ReceiptFooter = request.ReceiptFooter,
            CreatedAt = now,
            UpdatedAt = now
        });

        var settings = await context.Settings
            .FirstOrDefaultAsync(row => row.Id == MainRowId, cancellationToken);

        if (settings is null)
        {
            settings = new AppSettings { Id = MainRowId, CreatedAt = now };
            context.Settings.Add(settings);
        }

        settings.TaxEnabled = request.TaxEnabled;
        settings.TaxName = request.TaxName.Trim();
        settings.TaxRate = request.TaxRate;
        settings.TaxInclusive = request.TaxInclusive;
        settings.ServiceFeeEnabled = request.ServiceFeeEnabled;
        settings.CashRounding = request.CashRounding;
        settings.InvoicePrefix = request.InvoicePrefix.Trim();
        settings.Timezone = request.Timezone.Trim();
        settings.UtcOffsetMinutes = request.UtcOffsetMinutes;
        settings.UpdatedAt = now;

        var ownerRole = CreateRole(PermissionCatalog.OwnerRoleName, 1, now);
        var adminRole = CreateRole(PermissionCatalog.AdminRoleName, 2, now);
        var cashierRole = CreateRole(PermissionCatalog.CashierRoleName, 3, now);

        context.Roles.AddRange(ownerRole, adminRole, cashierRole);

        AddDefaultPermissions(context, ownerRole.Id, PermissionCatalog.OwnerRoleName, now);
        AddDefaultPermissions(context, adminRole.Id, PermissionCatalog.AdminRoleName, now);
        AddDefaultPermissions(context, cashierRole.Id, PermissionCatalog.CashierRoleName, now);

        context.Users.Add(new User
        {
            RoleId = ownerRole.Id,
            Name = request.OwnerFullName.Trim(),
            Username = request.OwnerUsername.Trim(),
            PasswordHash = _passwordHasher.Hash(request.OwnerPassword),
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        });

        context.AppLicenses.Add(new AppLicense
        {
            Id = MainRowId,
            Edition = "OFFLINE",
            Status = LicenseStatus.TRIAL,
            CreatedAt = now,
            UpdatedAt = now
        });

        await context.SaveChangesAsync(cancellationToken);

        await _activityLogService.LogAsync(
            "SETUP_COMPLETED",
            $"Setup awal usaha '{request.BusinessName.Trim()}' selesai dan akun owner '{request.OwnerUsername.Trim()}' dibuat.",
            cancellationToken: cancellationToken);
    }

    private static Role CreateRole(string name, int levelTier, DateTimeOffset now) => new()
    {
        Name = name,
        LevelTier = levelTier,
        IsSystem = true,
        CreatedAt = now,
        UpdatedAt = now
    };

    private static void AddDefaultPermissions(
        IKingraPosDbContext context,
        string roleId,
        string roleName,
        DateTimeOffset now)
    {
        foreach (var permissionKey in PermissionCatalog.GetDefaultPermissions(roleName))
        {
            context.RolePermissions.Add(new RolePermission
            {
                RoleId = roleId,
                PermissionKey = permissionKey,
                UpdatedAt = now
            });
        }
    }
}
