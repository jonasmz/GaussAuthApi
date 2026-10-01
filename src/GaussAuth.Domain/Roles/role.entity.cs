namespace GaussAuth.Domain.Roles;

public sealed class Role
{
    public Guid Id { get; private set; }
    public Guid ApplicationId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string NormalizedName { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private Role() { }

    private Role(Guid id, Guid applicationId, string name, string normalizedName, string? description, DateTimeOffset now)
    {
        if (id == Guid.Empty || applicationId == Guid.Empty) throw new ArgumentException("Role identifiers must be present.");
        if (string.IsNullOrWhiteSpace(name) || name.Length > 200) throw new ArgumentException("Role name is invalid.");
        if (description?.Length > 500) throw new ArgumentException("Role description is invalid.");
        Id = id; ApplicationId = applicationId; Name = name; NormalizedName = normalizedName; Description = description; IsActive = true; CreatedAt = now; UpdatedAt = now;
    }

    public static Role Create(Guid id, Guid applicationId, string name, string normalizedName, string? description, DateTimeOffset now) => new(id, applicationId, name, normalizedName, description, now);
    public void UpdateDescription(string? description, DateTimeOffset now) { if (description?.Length > 500) throw new ArgumentException("Role description is invalid."); if (Description != description) { Description = description; UpdatedAt = now; } }
    public void Activate(DateTimeOffset now) { if (!IsActive) { IsActive = true; UpdatedAt = now; } }
    public void Deactivate(DateTimeOffset now) { if (IsActive) { IsActive = false; UpdatedAt = now; } }
}
