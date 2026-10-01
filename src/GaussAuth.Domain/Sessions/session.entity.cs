namespace GaussAuth.Domain.Sessions;

public sealed class Session
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid ApplicationId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }

    private Session(Guid id, Guid userId, Guid applicationId, DateTimeOffset now, TimeSpan lifetime)
    {
        if (id == Guid.Empty || userId == Guid.Empty || applicationId == Guid.Empty) throw new ArgumentException("Session identifiers must be present.");
        if (lifetime <= TimeSpan.Zero) throw new ArgumentException("Session lifetime must be positive.");
        Id = id; UserId = userId; ApplicationId = applicationId; CreatedAt = now; ExpiresAt = now + lifetime;
    }
    private Session() { }
    public static Session Create(Guid id, Guid userId, Guid applicationId, DateTimeOffset now, TimeSpan lifetime) => new(id, userId, applicationId, now, lifetime);
    public void Revoke(DateTimeOffset now) { RevokedAt ??= now; }
    public SessionState GetState(DateTimeOffset now) => RevokedAt.HasValue ? SessionState.Revoked : now >= ExpiresAt ? SessionState.Expired : SessionState.Active;
}
