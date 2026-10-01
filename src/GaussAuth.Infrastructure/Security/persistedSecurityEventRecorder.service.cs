using GaussAuth.Application.Security;
using GaussAuth.Application.Security.Ports;
using GaussAuth.Domain.Security;
using Microsoft.Extensions.Logging;

namespace GaussAuth.Infrastructure.Security;

public sealed class PersistedSecurityEventRecorder(ISecurityEventRepository repository, SecurityEventCatalog catalog,
    TimeProvider timeProvider, ILogger<PersistedSecurityEventRecorder> logger) : ISecurityEventRecorder
{
    public Task RecordAsync(SecurityEventType type, Guid? userId, Guid? applicationId, Guid? sessionId,
        CancellationToken cancellationToken) => RecordAsync(new SecurityEventDraft(catalog.Get(type), userId, applicationId, sessionId), cancellationToken);

    public async Task RecordAsync(SecurityEventDraft draft, CancellationToken cancellationToken)
    {
        try
        {
            var definition = draft.Definition;
            var securityEvent = SecurityEvent.Create(Guid.NewGuid(), definition.EventType, ToStorageValue(definition.Outcome),
                timeProvider.GetUtcNow(), draft.UserId, draft.ApplicationId, draft.SessionId, draft.ConsumerApplicationId,
                draft.CorrelationId, draft.SubjectType, draft.SubjectId, draft.Reason, draft.Metadata);
            await repository.AddAsync(securityEvent, cancellationToken);
            await repository.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception) when (draft.Definition.Reliability == SecurityEventReliability.Operational)
        {
            logger.LogError(exception, "Operational security-event persistence failed for {EventType}.", draft.Definition.EventType);
        }
    }

    private static string ToStorageValue(SecurityEventOutcome outcome) => outcome switch
    {
        SecurityEventOutcome.Succeeded => "succeeded",
        SecurityEventOutcome.Rejected => "rejected",
        SecurityEventOutcome.Failed => "failed",
        _ => throw new ArgumentOutOfRangeException(nameof(outcome))
    };
}
