namespace GaussAuth.Application.AuthorizationContext;

public sealed record AuthorizationContext(
    Guid UserId,
    Guid ApplicationId,
    Guid SessionId,
    DateTimeOffset ExpiresAt,
    IReadOnlyList<AuthorizationContextRole> Roles,
    IReadOnlyList<string> Permissions);
