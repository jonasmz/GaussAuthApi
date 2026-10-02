namespace GaussAuth.Api.DependencyInjection;

/// <summary>Fixed-window limit for one endpoint group: at most <see cref="PermitLimit"/> requests per <see cref="WindowSeconds"/>.</summary>
public sealed class RateLimitPolicyOptions(int permitLimit, int windowSeconds)
{
    public int PermitLimit { get; } = permitLimit;

    public int WindowSeconds { get; } = windowSeconds;

    public TimeSpan Window => TimeSpan.FromSeconds(WindowSeconds);
}
