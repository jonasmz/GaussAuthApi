namespace GaussAuth.Domain.Security;

public sealed class SecurityEvent
{
    public Guid Id { get; private set; }
    public string EventType { get; private set; } = null!;
    public string Outcome { get; private set; } = null!;
    public DateTimeOffset OccurredAtUtc { get; private set; }
    public Guid? UserId { get; private set; }
    public Guid? ActorUserId { get; private set; }
    public Guid? ApplicationId { get; private set; }
    public Guid? SessionId { get; private set; }
    public Guid? ConsumerApplicationId { get; private set; }
    public string? CorrelationId { get; private set; }
    public string? SubjectType { get; private set; }
    public Guid? SubjectId { get; private set; }
    public string? Reason { get; private set; }
    public string? Metadata { get; private set; }

    private SecurityEvent() { }

    private SecurityEvent(Guid id, string eventType, string outcome, DateTimeOffset occurredAtUtc, Guid? userId,
        Guid? applicationId, Guid? sessionId, Guid? consumerApplicationId, string? correlationId,
        string? subjectType, Guid? subjectId, string? reason, string? metadata, Guid? actorUserId)
    {
        if (id == Guid.Empty) throw new ArgumentException("Security event identifier must be present.", nameof(id));
        if (string.IsNullOrWhiteSpace(eventType) || eventType.Length > 128) throw new ArgumentException("Security event type is invalid.", nameof(eventType));
        if (outcome is not ("succeeded" or "rejected" or "failed")) throw new ArgumentException("Security event outcome is invalid.", nameof(outcome));
        if (occurredAtUtc.Offset != TimeSpan.Zero) throw new ArgumentException("Security event timestamp must be UTC.", nameof(occurredAtUtc));
        if (correlationId?.Length > 128) throw new ArgumentException("Correlation identifier is too long.", nameof(correlationId));
        if (metadata?.Length > 2048) throw new ArgumentException("Security event metadata is too large.", nameof(metadata));
        EnsureSafeContext(correlationId, nameof(correlationId));
        EnsureSafeContext(subjectType, nameof(subjectType));
        EnsureSafeContext(reason, nameof(reason));
        EnsureSafeContext(metadata, nameof(metadata));

        Id = id; EventType = eventType; Outcome = outcome; OccurredAtUtc = occurredAtUtc; UserId = userId;
        ApplicationId = applicationId; SessionId = sessionId; ConsumerApplicationId = consumerApplicationId;
        CorrelationId = correlationId; SubjectType = subjectType; SubjectId = subjectId; Reason = reason; Metadata = metadata;
        ActorUserId = actorUserId;
    }

    public static SecurityEvent Create(Guid id, string eventType, string outcome, DateTimeOffset occurredAtUtc,
        Guid? userId = null, Guid? applicationId = null, Guid? sessionId = null, Guid? consumerApplicationId = null,
        string? correlationId = null, string? subjectType = null, Guid? subjectId = null, string? reason = null,
        string? metadata = null, Guid? actorUserId = null) => new(id, eventType, outcome, occurredAtUtc, userId, applicationId, sessionId,
        consumerApplicationId, correlationId, subjectType, subjectId, reason, metadata, actorUserId);

    private static void EnsureSafeContext(string? value, string parameterName)
    {
        if (value is null) return;
        var prohibited = new[] { "password", "secret", "token", "authorization", "cookie", "securitystamp", "private key", "connection string", "hash" };
        if (prohibited.Any(term => value.Contains(term, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ArgumentException("Security event context contains prohibited material.", parameterName);
        }
    }
}
