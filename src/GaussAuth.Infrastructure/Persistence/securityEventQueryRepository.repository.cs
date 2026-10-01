using GaussAuth.Application.Security;
using GaussAuth.Application.Security.Ports;
using GaussAuth.Domain.Security;
using Microsoft.EntityFrameworkCore;

namespace GaussAuth.Infrastructure.Persistence;

public sealed class SecurityEventQueryRepository(AuthenticationDbContext context) : ISecurityEventQueryRepository
{
    public async Task<IReadOnlyList<SecurityEvent>> ListAsync(SecurityEventQuery query, Guid? forcedApplicationId,
        bool includeGlobalEvents, CancellationToken cancellationToken)
    {
        IQueryable<SecurityEvent> events = context.SecurityEvents.AsNoTracking();
        if (includeGlobalEvents)
        {
            if (query.ApplicationId is { } applicationId) events = events.Where(item => item.ApplicationId == applicationId);
        }
        else if (forcedApplicationId is { } applicationId)
        {
            events = events.Where(item => item.ApplicationId == applicationId);
        }
        else return [];

        if (query.FromUtc is { } fromUtc) events = events.Where(item => item.OccurredAtUtc >= fromUtc);
        if (query.ToUtc is { } toUtc) events = events.Where(item => item.OccurredAtUtc <= toUtc);
        if (!string.IsNullOrWhiteSpace(query.EventType)) events = events.Where(item => item.EventType == query.EventType);
        if (query.Outcome is { } outcome) events = events.Where(item => item.Outcome == outcome.ToString().ToLowerInvariant());
        if (query.UserId is { } userId) events = events.Where(item => item.UserId == userId);
        if (query.SessionId is { } sessionId) events = events.Where(item => item.SessionId == sessionId);
        if (query.Cursor is not null && SecurityEventQueryService.TryDecodeCursor(query.Cursor, out var cursor))
            events = events.Where(item => item.OccurredAtUtc < cursor.OccurredAtUtc || item.OccurredAtUtc == cursor.OccurredAtUtc && item.Id.CompareTo(cursor.Id) < 0);

        return await events.OrderByDescending(item => item.OccurredAtUtc).ThenByDescending(item => item.Id)
            .Take(query.PageSize + 1).ToListAsync(cancellationToken);
    }
}
