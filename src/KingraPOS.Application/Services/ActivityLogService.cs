using KingraPOS.Application.Abstractions.Persistence;
using KingraPOS.Application.Abstractions.Security;
using KingraPOS.Application.Abstractions.Services;
using KingraPOS.Domain.Entities;

namespace KingraPOS.Application.Services;

public sealed class ActivityLogService : IActivityLogService
{
    private readonly IKingraPosDbContextFactory _contextFactory;
    private readonly IClock _clock;

    public ActivityLogService(IKingraPosDbContextFactory contextFactory, IClock clock)
    {
        _contextFactory = contextFactory;
        _clock = clock;
    }

    public async Task LogAsync(
        string actionType,
        string description,
        string? userId = null,
        string? entityType = null,
        string? entityId = null,
        string? payload = null,
        CancellationToken cancellationToken = default)
    {
        using var context = _contextFactory.Create();

        context.ActivityLogs.Add(new ActivityLog
        {
            ActionType = actionType,
            Description = description,
            UserId = userId,
            EntityType = entityType,
            EntityId = entityId,
            Payload = payload,
            CreatedAt = _clock.UtcNow
        });

        await context.SaveChangesAsync(cancellationToken);
    }
}
