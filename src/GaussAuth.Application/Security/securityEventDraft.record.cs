namespace GaussAuth.Application.Security;

public sealed record SecurityEventDraft(SecurityEventDefinition Definition, Guid? UserId, Guid? ApplicationId,
    Guid? SessionId, Guid? ConsumerApplicationId = null, string? CorrelationId = null, string? SubjectType = null,
    Guid? SubjectId = null, string? Reason = null, string? Metadata = null, Guid? ActorUserId = null);
