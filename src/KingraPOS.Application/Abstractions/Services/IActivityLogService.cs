namespace KingraPOS.Application.Abstractions.Services;

public interface IActivityLogService
{
    Task LogAsync(
        string actionType,
        string description,
        string? userId = null,
        string? entityType = null,
        string? entityId = null,
        string? payload = null,
        CancellationToken cancellationToken = default);
}
