namespace GaussAuth.Domain.Memberships;

public sealed class ApplicationMembership
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid ApplicationId { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private ApplicationMembership(Guid id, Guid userId, Guid applicationId, bool isActive, DateTimeOffset now)
    {
        if (id == Guid.Empty || userId == Guid.Empty || applicationId == Guid.Empty) throw new ArgumentException("Membership identifiers must be present.");
        Id = id; UserId = userId; ApplicationId = applicationId; IsActive = isActive; CreatedAt = now; UpdatedAt = now;
    }
    private ApplicationMembership() { }
    public static ApplicationMembership Create(Guid id, Guid userId, Guid applicationId, bool isActive, DateTimeOffset now) => new(id, userId, applicationId, isActive, now);
    public void Activate(DateTimeOffset now) { if (!IsActive) { IsActive = true; UpdatedAt = now; } }
    public void Deactivate(DateTimeOffset now) { if (IsActive) { IsActive = false; UpdatedAt = now; } }
}
