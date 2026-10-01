namespace GaussAuth.Application.Sessions;

public sealed class SessionOperationResult
{
    public bool IsSuccess { get; private init; }
    public Guid? SessionId { get; private init; }
    public Guid? UserId { get; private init; }
    public Guid? ApplicationId { get; private init; }
    public string? AccessCredential { get; private init; }
    public DateTimeOffset? AccessCredentialIssuedAt { get; private init; }
    public DateTimeOffset? AccessCredentialExpiresAt { get; private init; }
    public DateTimeOffset? SessionExpiresAt { get; private init; }
    internal SessionRejectionReason? Reason { get; private init; }

    internal static SessionOperationResult Success(Guid sessionId, Guid userId, Guid applicationId, string? accessCredential, DateTimeOffset? accessCredentialIssuedAt, DateTimeOffset? accessCredentialExpiresAt, DateTimeOffset? sessionExpiresAt) =>
        new() { IsSuccess = true, SessionId = sessionId, UserId = userId, ApplicationId = applicationId, AccessCredential = accessCredential, AccessCredentialIssuedAt = accessCredentialIssuedAt, AccessCredentialExpiresAt = accessCredentialExpiresAt, SessionExpiresAt = sessionExpiresAt };

    internal static SessionOperationResult Failure(SessionRejectionReason reason) => new() { IsSuccess = false, Reason = reason };
}
