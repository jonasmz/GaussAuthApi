namespace GaussAuth.Domain.Applications;

public sealed class Application
{
    public Guid Id { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private Application(Guid id, string code, string name, DateTimeOffset now)
    {
        if (id == Guid.Empty || string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Application values must be present.");
        Id = id; Code = code; Name = name; IsActive = true; CreatedAt = now; UpdatedAt = now;
    }
    private Application() { }
    public static Application Create(Guid id, string code, string name, DateTimeOffset now) => new(id, code, name, now);
    public void Activate(DateTimeOffset now) { if (!IsActive) { IsActive = true; UpdatedAt = now; } }
    public void Deactivate(DateTimeOffset now) { if (IsActive) { IsActive = false; UpdatedAt = now; } }
}
