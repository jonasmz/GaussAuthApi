using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace GaussAuth.Api.DependencyInjection;

public sealed class ReadinessResultCache
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(5);
    private readonly SemaphoreSlim gate = new(1, 1);
    private HealthReport? report;
    private DateTimeOffset expiresAt;

    public async Task<HealthReport> GetAsync(HealthCheckService healthChecks, CancellationToken cancellationToken)
    {
        if (report is { } current && DateTimeOffset.UtcNow < expiresAt) return current;

        await gate.WaitAsync(cancellationToken);
        try
        {
            if (report is { } cached && DateTimeOffset.UtcNow < expiresAt) return cached;
            report = await healthChecks.CheckHealthAsync(cancellationToken);
            expiresAt = DateTimeOffset.UtcNow.Add(Lifetime);
            return report;
        }
        finally
        {
            gate.Release();
        }
    }
}
