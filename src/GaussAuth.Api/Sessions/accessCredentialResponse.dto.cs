namespace GaussAuth.Api.Sessions;

public sealed record AccessCredentialResponse(Guid SessionId, string TokenType, string AccessToken, DateTimeOffset ExpiresAt, DateTimeOffset SessionExpiresAt);
