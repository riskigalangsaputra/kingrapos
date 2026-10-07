using KingraPOS.Application.Abstractions.Persistence;
using KingraPOS.Application.Abstractions.Security;
using KingraPOS.Application.Abstractions.Services;
using KingraPOS.Application.Dtos;
using KingraPOS.Application.Sales;
using KingraPOS.Application.Security;
using KingraPOS.Domain.Entities;
using KingraPOS.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace KingraPOS.Application.Services;

public sealed class ShiftService : IShiftService
{
    private readonly IKingraPosDbContextFactory _contextFactory;
    private readonly IPermissionService _permissionService;
    private readonly ICurrentUserSession _session;
    private readonly IActivityLogService _activityLogService;
    private readonly ILicenseService _licenseService;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IClock _clock;

    public ShiftService(
        IKingraPosDbContextFactory contextFactory,
        IPermissionService permissionService,
        ICurrentUserSession session,
        IActivityLogService activityLogService,
        ILicenseService licenseService,
        IPasswordHasher passwordHasher,
        IClock clock)
    {
        _contextFactory = contextFactory;
        _permissionService = permissionService;
        _session = session;
        _activityLogService = activityLogService;
        _licenseService = licenseService;
        _passwordHasher = passwordHasher;
        _clock = clock;
    }

    public async Task<ShiftDto?> GetOpenShiftAsync(CancellationToken cancellationToken = default)
    {
        EnsurePermission(PermissionCatalog.ShiftOpen);

        using var context = _contextFactory.Create();

        var shift = await context.CashierShifts
            .FirstOrDefaultAsync(row => row.Status == ShiftStatus.OPEN, cancellationToken);

        return shift is null ? null : await ToDtoAsync(context, shift, cancellationToken);
    }

    public async Task<IReadOnlyList<ShiftDto>> GetRecentAsync(
        int take = 50,
        CancellationToken cancellationToken = default)
    {
        EnsurePermission(PermissionCatalog.ShiftClose);

        using var context = _contextFactory.Create();

        var shifts = await context.CashierShifts
            .OrderByDescending(row => row.StartTime)
            .Take(take)
            .ToListAsync(cancellationToken);

        var result = new List<ShiftDto>(shifts.Count);

        foreach (var shift in shifts)
            result.Add(await ToDtoAsync(context, shift, cancellationToken));

        return result;
    }

    public async Task<ShiftDto> OpenAsync(OpenShiftRequest request, CancellationToken cancellationToken = default)
    {
        EnsurePermission(PermissionCatalog.ShiftOpen);
        await _licenseService.EnsureWriteAllowedAsync(cancellationToken);

        using var context = _contextFactory.Create();

        var existing = await context.CashierShifts
            .FirstOrDefaultAsync(row => row.Status == ShiftStatus.OPEN, cancellationToken);

        if (existing is not null)
            throw new InvalidOperationException("Masih ada shift yang terbuka. Tutup shift tersebut sebelum membuka shift baru.");

        var now = _clock.UtcNow;

        var shift = new CashierShift
        {
            UserId = RequireUserId(),
            StartTime = now,
            CashDrawerStart = request.CashDrawerStart,
            Status = ShiftStatus.OPEN,
            Notes = Normalize(request.Notes),
            CreatedAt = now,
            UpdatedAt = now
        };

        context.CashierShifts.Add(shift);
        await context.SaveChangesAsync(cancellationToken);

        await _activityLogService.LogAsync(
            "SHIFT_OPENED",
            $"Shift dibuka dengan modal awal {request.CashDrawerStart}.",
            userId: _session.User?.UserId,
            entityType: "cashier_shifts",
            entityId: shift.Id,
            cancellationToken: cancellationToken);

        return await ToDtoAsync(context, shift, cancellationToken);
    }

    public async Task<ShiftDto> CloseAsync(CloseShiftRequest request, CancellationToken cancellationToken = default)
    {
        EnsurePermission(PermissionCatalog.ShiftClose);
        await _licenseService.EnsureWriteAllowedAsync(cancellationToken);

        using var context = _contextFactory.Create();

        var shift = await LoadOpenShiftAsync(context, request.ShiftId, cancellationToken);

        await CloseShiftInternalAsync(context, shift, request.CashDrawerEndPhysical, request.Notes, cancellationToken);

        await context.SaveChangesAsync(cancellationToken);

        await _activityLogService.LogAsync(
            "SHIFT_CLOSED",
            $"Shift ditutup. Selisih kas {shift.DiscrepancyAmount}.",
            userId: _session.User?.UserId,
            entityType: "cashier_shifts",
            entityId: shift.Id,
            cancellationToken: cancellationToken);

        return await ToDtoAsync(context, shift, cancellationToken);
    }

    public async Task<ShiftDto> SwitchAsync(SwitchShiftRequest request, CancellationToken cancellationToken = default)
    {
        EnsurePermission(PermissionCatalog.ShiftClose);
        EnsurePermission(PermissionCatalog.ShiftOpen);
        await _licenseService.EnsureWriteAllowedAsync(cancellationToken);

        using var context = _contextFactory.Create();

        await EnsurePinAsync(context, request.Pin, cancellationToken);

        var current = await context.CashierShifts
            .FirstOrDefaultAsync(row => row.Status == ShiftStatus.OPEN, cancellationToken)
            ?? throw new InvalidOperationException("Tidak ada shift terbuka yang bisa diganti.");

        await CloseShiftInternalAsync(context, current, request.CashDrawerEndPhysical, request.Notes, cancellationToken);

        var now = _clock.UtcNow;

        var next = new CashierShift
        {
            UserId = RequireUserId(),
            StartTime = now,
            CashDrawerStart = request.NewCashDrawerStart,
            Status = ShiftStatus.OPEN,
            Notes = Normalize(request.Notes),
            CreatedAt = now,
            UpdatedAt = now
        };

        context.CashierShifts.Add(next);
        await context.SaveChangesAsync(cancellationToken);

        await _activityLogService.LogAsync(
            "SHIFT_SWITCHED",
            $"Shift diganti. Modal awal shift baru {request.NewCashDrawerStart}.",
            userId: _session.User?.UserId,
            entityType: "cashier_shifts",
            entityId: next.Id,
            cancellationToken: cancellationToken);

        return await ToDtoAsync(context, next, cancellationToken);
    }

    private async Task CloseShiftInternalAsync(
        IKingraPosDbContext context,
        CashierShift shift,
        long physicalCash,
        string? notes,
        CancellationToken cancellationToken)
    {
        var cashSales = await GetCashSalesAsync(context, shift.Id, cancellationToken);
        var systemCash = shift.CashDrawerStart + cashSales;
        var now = _clock.UtcNow;

        shift.CashDrawerEndSystem = systemCash;
        shift.CashDrawerEndPhysical = physicalCash;
        shift.DiscrepancyAmount = physicalCash - systemCash;
        shift.Status = ShiftStatus.CLOSED;
        shift.EndTime = now;
        shift.UpdatedAt = now;

        if (!string.IsNullOrWhiteSpace(notes))
            shift.Notes = Normalize(notes);
    }

    private static async Task<CashierShift> LoadOpenShiftAsync(
        IKingraPosDbContext context,
        string shiftId,
        CancellationToken cancellationToken)
    {
        var shift = await context.CashierShifts
            .FirstOrDefaultAsync(row => row.Id == shiftId, cancellationToken)
            ?? throw new InvalidOperationException("Shift tidak ditemukan.");

        if (shift.Status != ShiftStatus.OPEN)
            throw new InvalidOperationException("Shift tersebut sudah ditutup.");

        return shift;
    }

    private static async Task<long> GetCashSalesAsync(
        IKingraPosDbContext context,
        string shiftId,
        CancellationToken cancellationToken)
    {
        return await context.Transactions
            .Where(row => row.ShiftId == shiftId
                          && row.Status == TransactionStatus.COMPLETED
                          && row.PaymentMethod == PaymentMethods.Cash)
            .SumAsync(row => (long?)row.TotalNetAmount, cancellationToken) ?? 0;
    }

    private async Task EnsurePinAsync(
        IKingraPosDbContext context,
        string? pin,
        CancellationToken cancellationToken)
    {
        var userId = RequireUserId();

        var pinHash = await context.Users
            .Where(user => user.Id == userId)
            .Select(user => user.PinHash)
            .FirstOrDefaultAsync(cancellationToken);

        // PIN hanya diwajibkan bila pengguna sudah mengatur PIN-nya.
        if (string.IsNullOrWhiteSpace(pinHash))
            return;

        if (string.IsNullOrWhiteSpace(pin) || !_passwordHasher.Verify(pin, pinHash))
            throw new UnauthorizedAccessException("PIN salah.");
    }

    private static async Task<ShiftDto> ToDtoAsync(
        IKingraPosDbContext context,
        CashierShift shift,
        CancellationToken cancellationToken)
    {
        var userName = await context.Users
            .Where(user => user.Id == shift.UserId)
            .Select(user => user.Name)
            .FirstOrDefaultAsync(cancellationToken) ?? "-";

        var cashSales = await GetCashSalesAsync(context, shift.Id, cancellationToken);

        var salesCount = await context.Transactions
            .CountAsync(row => row.ShiftId == shift.Id && row.Status == TransactionStatus.COMPLETED, cancellationToken);

        return new ShiftDto(
            shift.Id,
            shift.UserId,
            userName,
            shift.StartTime,
            shift.EndTime,
            shift.CashDrawerStart,
            shift.CashDrawerEndSystem,
            shift.CashDrawerEndPhysical,
            shift.DiscrepancyAmount,
            shift.Status,
            shift.Notes,
            cashSales,
            salesCount);
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private string RequireUserId() =>
        _session.User?.UserId ?? throw new InvalidOperationException("Sesi pengguna tidak ditemukan.");

    private void EnsurePermission(string permissionKey)
    {
        if (!_permissionService.HasPermission(permissionKey))
            throw new UnauthorizedAccessException($"Anda tidak memiliki izin '{permissionKey}'.");
    }
}
