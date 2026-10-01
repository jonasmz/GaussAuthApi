namespace GaussAuth.Domain.Authorization;

public sealed class UserRole
{
    public Guid Id { get; private set; }
    public Guid ApplicationId { get; private set; }
    public Guid UserId { get; private set; }
    public Guid RoleId { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private UserRole() { }
    private UserRole(Guid id, Guid applicationId, Guid userId, Guid roleId, DateTimeOffset now)
    { if (id == Guid.Empty || applicationId == Guid.Empty || userId == Guid.Empty || roleId == Guid.Empty) throw new ArgumentException("User role identifiers must be present."); Id = id; ApplicationId = applicationId; UserId = userId; RoleId = roleId; IsActive = true; CreatedAt = now; UpdatedAt = now; }
    public static UserRole Create(Guid id, Guid applicationId, Guid userId, Guid roleId, DateTimeOffset now) => new(id, applicationId, userId, roleId, now);
    public void Activate(DateTimeOffset now) { if (!IsActive) { IsActive = true; UpdatedAt = now; } }
    public void Deactivate(DateTimeOffset now) { if (IsActive) { IsActive = false; UpdatedAt = now; } }
}
