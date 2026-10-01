namespace GaussAuth.Application.Security;

public sealed record SecurityEventQuery(DateTimeOffset? FromUtc = null, DateTimeOffset? ToUtc = null,
    string? EventType = null, SecurityEventOutcome? Outcome = null, Guid? UserId = null, Guid? ApplicationId = null,
    Guid? SessionId = null, int PageSize = 50, string? Cursor = null);
