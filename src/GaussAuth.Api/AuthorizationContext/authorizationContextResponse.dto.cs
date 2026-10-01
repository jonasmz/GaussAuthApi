namespace GaussAuth.Api.AuthorizationContext;

public sealed record AuthorizationContextResponse(
    Guid UserId,
    Guid ApplicationId,
    Guid SessionId,
    DateTimeOffset IssuedAt,
    DateTimeOffset CredentialExpiresAt,
    DateTimeOffset SessionExpiresAt,
    IReadOnlyList<AuthorizationContextRoleResponse> Roles,
    IReadOnlyList<string> Permissions);
