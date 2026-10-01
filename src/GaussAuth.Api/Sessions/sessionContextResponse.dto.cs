namespace GaussAuth.Api.Sessions;

public sealed record SessionContextResponse(Guid UserId, Guid ApplicationId, Guid SessionId, DateTimeOffset ExpiresAt);
