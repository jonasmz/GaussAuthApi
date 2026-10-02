namespace GaussAuth.Api.Security;

public sealed record SecurityEventResponse(Guid Id, string EventType, string Outcome, DateTimeOffset OccurredAtUtc,
    Guid? UserId, Guid? ApplicationId, Guid? SessionId, Guid? ConsumerApplicationId, string? CorrelationId,
    string? SubjectType, Guid? SubjectId, string? Reason, Guid? ActorUserId);
