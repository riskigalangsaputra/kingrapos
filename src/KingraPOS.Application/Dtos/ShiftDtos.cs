using KingraPOS.Domain.Enums;

namespace KingraPOS.Application.Dtos;

public record ShiftDto(
    string Id,
    string UserId,
    string UserName,
    DateTimeOffset StartTime,
    DateTimeOffset? EndTime,
    long CashDrawerStart,
    long? CashDrawerEndSystem,
    long? CashDrawerEndPhysical,
    long? DiscrepancyAmount,
    ShiftStatus Status,
    string? Notes,
    long CashSalesAmount,
    int SalesCount);

public record OpenShiftRequest(long CashDrawerStart, string? Notes = null);

public record CloseShiftRequest(string ShiftId, long CashDrawerEndPhysical, string? Notes = null);

public record SwitchShiftRequest(
    long NewCashDrawerStart,
    long CashDrawerEndPhysical,
    string? Pin = null,
    string? Notes = null);
