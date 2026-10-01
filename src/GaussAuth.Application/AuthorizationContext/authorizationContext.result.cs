namespace GaussAuth.Application.AuthorizationContext;

public sealed record AuthorizationContext(
    Guid UserId,
    Guid ApplicationId,
    Guid SessionId,
    DateTimeOffset IssuedAt,
    DateTimeOffset CredentialExpiresAt,
    DateTimeOffset SessionExpiresAt,
    IReadOnlyList<AuthorizationContextRole> Roles,
    IReadOnlyList<string> Permissions);
