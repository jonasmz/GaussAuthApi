using GaussAuth.Infrastructure.ProfileImages;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace GaussAuth.Infrastructure.Health;

public sealed class ProfileStorageReadinessCheck(
    ProfileImagesOptions options,
    ILogger<ProfileStorageReadinessCheck> logger) : IHealthCheck
{
    private const string HealthyCause = "Profile storage readiness restored.";
    private const string UnhealthyCause = "Profile storage readiness check failed.";
    private int lastHealthy = -1;

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(2));
            var probePath = Path.Combine(options.RootPath, ".gaussauth-readiness-" + Guid.NewGuid().ToString("N"));

            await Task.Run(() =>
            {
                Directory.CreateDirectory(options.RootPath);
                using (File.Create(probePath, 1, FileOptions.DeleteOnClose)) { }
                File.Delete(probePath);
            }, timeout.Token);

            LogTransition(true);
            return HealthCheckResult.Healthy();
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            LogTransition(false);
            return HealthCheckResult.Unhealthy("Profile storage is not ready.");
        }
        catch (Exception)
        {
            LogTransition(false);
            return HealthCheckResult.Unhealthy("Profile storage is not ready.");
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
