namespace GaussAuth.Api.Login;

public sealed record LoginResponse(
    Guid UserId,
    Guid ApplicationId,
    Guid SessionId,
    string TokenType,
    string AccessToken,
    DateTimeOffset ExpiresAt,
    DateTimeOffset SessionExpiresAt);
