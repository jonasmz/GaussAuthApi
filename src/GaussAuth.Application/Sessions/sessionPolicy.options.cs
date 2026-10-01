namespace GaussAuth.Application.Sessions;

public sealed record SessionPolicy
{
    public TimeSpan SessionLifetime { get; }
    public TimeSpan AccessCredentialLifetime { get; }

    public SessionPolicy(TimeSpan sessionLifetime, TimeSpan accessCredentialLifetime)
    {
        if (sessionLifetime <= TimeSpan.Zero || accessCredentialLifetime <= TimeSpan.Zero || accessCredentialLifetime > sessionLifetime)
            throw new ArgumentException("Session lifetimes are invalid.");
        SessionLifetime = sessionLifetime; AccessCredentialLifetime = accessCredentialLifetime;
    }
}
