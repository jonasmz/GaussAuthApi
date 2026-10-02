using GaussAuth.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace GaussAuth.Infrastructure.Health;

public sealed class DatabaseReadinessCheck(
    IServiceScopeFactory scopeFactory,
    ILogger<DatabaseReadinessCheck> logger) : IHealthCheck
{
    private const string HealthyCause = "Database readiness restored.";
    private const string UnhealthyCause = "Database readiness check failed.";
    private int lastHealthy = -1;

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(2));
            await using var scope = scopeFactory.CreateAsyncScope();
            var database = scope.ServiceProvider.GetRequiredService<AuthenticationDbContext>();

            if (!await database.Database.CanConnectAsync(timeout.Token) ||
                (await database.Database.GetPendingMigrationsAsync(timeout.Token)).Any())
            {
                LogTransition(false);
                return HealthCheckResult.Unhealthy("Database is not ready.");
            }

            LogTransition(true);
            return HealthCheckResult.Healthy();
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            LogTransition(false);
            return HealthCheckResult.Unhealthy("Database is not ready.");
        }
        catch (Exception)
        {
            LogTransition(false);
            return HealthCheckResult.Unhealthy("Database is not ready.");
        }
    }

    private void LogTransition(bool healthy)
    {
        var previous = Interlocked.Exchange(ref lastHealthy, healthy ? 1 : 0);
        if (previous == (healthy ? 1 : 0)) return;

        if (healthy) logger.LogInformation("{ReadinessCause}", HealthyCause);
        else logger.LogWarning("{ReadinessCause}", UnhealthyCause);
    }
}
