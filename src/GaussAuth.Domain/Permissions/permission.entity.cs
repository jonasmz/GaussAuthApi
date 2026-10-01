namespace GaussAuth.Domain.Permissions;

public sealed class Permission
{
    public Guid Id { get; private set; }
    public Guid ApplicationId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private Permission() { }

    private Permission(Guid id, Guid applicationId, string code, string? description, DateTimeOffset now)
    {
        if (id == Guid.Empty || applicationId == Guid.Empty) throw new ArgumentException("Permission identifiers must be present.");
        if (string.IsNullOrWhiteSpace(code) || code.Length > 128) throw new ArgumentException("Permission code is invalid.");
        if (description?.Length > 500) throw new ArgumentException("Permission description is invalid.");
        Id = id; ApplicationId = applicationId; Code = code; Description = description; IsActive = true; CreatedAt = now; UpdatedAt = now;
    }

    public static Permission Create(Guid id, Guid applicationId, string code, string? description, DateTimeOffset now) => new(id, applicationId, code, description, now);
    public void UpdateDescription(string? description, DateTimeOffset now) { if (description?.Length > 500) throw new ArgumentException("Permission description is invalid."); if (Description != description) { Description = description; UpdatedAt = now; } }
    public void Activate(DateTimeOffset now) { if (!IsActive) { IsActive = true; UpdatedAt = now; } }
    public void Deactivate(DateTimeOffset now) { if (IsActive) { IsActive = false; UpdatedAt = now; } }
}
