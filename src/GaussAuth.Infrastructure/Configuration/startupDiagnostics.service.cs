using GaussAuth.Application.Passwords.Ports;
using GaussAuth.Application.Sessions;
using GaussAuth.Infrastructure.ProfileImages;
using GaussAuth.Infrastructure.Sessions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GaussAuth.Infrastructure.Configuration;

/// <summary>
/// Logs one non-sensitive startup summary, the Production-class operational warnings, and shutdown start/stop. It
/// never logs secrets, key material, file paths or connection strings.
/// </summary>
public sealed class StartupDiagnostics(
    ILogger<StartupDiagnostics> logger,
    IHostEnvironment environment,
    IHostApplicationLifetime lifetime,
    ProfileImagesOptions profileImages,
    KeyRingOptions keyRing,
    AccessCredentialSigningKey signingKey,
    SessionPolicy sessionPolicy,
    IServiceScopeFactory scopeFactory) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var recoveryAvailable = scope.ServiceProvider.GetRequiredService<IRecoveryDelivery>().IsAvailable;
        var productionClass = environment.IsProductionClass();

        logger.LogInformation(
            "Starting {Application} in environment {Environment} (production-class: {ProductionClass}). " +
            "Session lifetime {SessionMinutes} min, access credential lifetime {AccessMinutes} min, " +
            "profile storage persistence declared {ProfileStoragePersistent}, key ring persisted {KeyRingPersisted}, " +
            "password recovery delivery available {RecoveryDeliveryAvailable}.",
            environment.ApplicationName,
            environment.EnvironmentName,
            productionClass,
            (int)sessionPolicy.SessionLifetime.TotalMinutes,
            (int)sessionPolicy.AccessCredentialLifetime.TotalMinutes,
            profileImages.StorageIsPersistent?.ToString() ?? "undeclared",
            keyRing.IsPersisted,
            recoveryAvailable);

        if (signingKey.IsEphemeral)
        {
            logger.LogWarning(
                "No access credential signing key is configured; using an ephemeral development key. Issued credentials stop validating when the process restarts.");
        }

        if (productionClass)
        {
            if (profileImages.StorageIsPersistent == false)
            {
                logger.LogWarning(
                    "Profile image storage is declared non-persistent. Profile images may be lost when the container is replaced or recreated.");
            }

            if (!recoveryAvailable)
            {
                logger.LogWarning(
                    "Password recovery delivery is not configured. Recovery requests receive the generic response but no reset credential " +
                    "is issued; supply a recovery delivery adapter to enable password recovery.");
            }
        }

        lifetime.ApplicationStopping.Register(() => logger.LogInformation("Shutdown started: no new requests are accepted and in-flight requests are draining."));
        lifetime.ApplicationStopped.Register(() => logger.LogInformation("Shutdown completed."));
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
