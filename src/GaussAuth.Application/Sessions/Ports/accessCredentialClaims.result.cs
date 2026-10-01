namespace GaussAuth.Application.Sessions.Ports;

public sealed record AccessCredentialClaims(Guid SessionId, Guid UserId, Guid ApplicationId, DateTimeOffset IssuedAt, DateTimeOffset ExpiresAt);
