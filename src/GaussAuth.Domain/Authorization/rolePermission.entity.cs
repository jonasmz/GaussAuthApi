namespace GaussAuth.Domain.Authorization;

public sealed class RolePermission
{
    public Guid Id { get; private set; }
    public Guid ApplicationId { get; private set; }
    public Guid RoleId { get; private set; }
    public Guid PermissionId { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private RolePermission() { }
    private RolePermission(Guid id, Guid applicationId, Guid roleId, Guid permissionId, DateTimeOffset now)
    { if (id == Guid.Empty || applicationId == Guid.Empty || roleId == Guid.Empty || permissionId == Guid.Empty) throw new ArgumentException("Role permission identifiers must be present."); Id = id; ApplicationId = applicationId; RoleId = roleId; PermissionId = permissionId; IsActive = true; CreatedAt = now; UpdatedAt = now; }
    public static RolePermission Create(Guid id, Guid applicationId, Guid roleId, Guid permissionId, DateTimeOffset now) => new(id, applicationId, roleId, permissionId, now);
    public void Activate(DateTimeOffset now) { if (!IsActive) { IsActive = true; UpdatedAt = now; } }
    public void Deactivate(DateTimeOffset now) { if (IsActive) { IsActive = false; UpdatedAt = now; } }
}
