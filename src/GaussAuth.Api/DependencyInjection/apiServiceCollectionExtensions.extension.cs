using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.RateLimiting;

namespace GaussAuth.Api.DependencyInjection;

public static class ApiServiceCollectionExtensions
{
    public static IServiceCollection AddApiServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddProblemDetails();
        services.AddExceptionHandler<SafeExceptionHandler>();
        services.AddReadinessChecks();

        var limits = RateLimitOptions.Load(configuration);

        services.Configure<FormOptions>(options =>
        {
            options.ValueCountLimit = 4;
            options.MultipartHeadersCountLimit = 8;
            options.MultipartHeadersLengthLimit = 4096;
            options.MultipartBoundaryLengthLimit = 128;
            options.BufferBody = false;
            options.MemoryBufferThreshold = 64 * 1024;
        });

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = (context, cancellationToken) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter = ((int)retryAfter.TotalSeconds).ToString();
                }

                return ValueTask.CompletedTask;
            };

            AddFixedWindowPolicy(options, "user-creation", limits.UserCreation);
            AddFixedWindowPolicy(options, "login", limits.Login);
            AddFixedWindowPolicy(options, "signing-keys", limits.SigningKeys);
            AddFixedWindowPolicy(options, "session-credentials", limits.SessionCredentials);
            AddFixedWindowPolicy(options, "password-recovery", limits.PasswordRecovery);
            AddFixedWindowPolicy(options, "password-reset", limits.PasswordReset);
            AddFixedWindowPolicy(options, "authorization-context", limits.AuthorizationContext);
            AddFixedWindowPolicy(options, "profile-image-write", limits.ProfileImageWrite);
            AddFixedWindowPolicy(options, "administration", limits.Administration);
            AddFixedWindowPolicy(options, "security-events", limits.SecurityEvents);
        });

        return services;
    }

    private static void AddFixedWindowPolicy(RateLimiterOptions options, string name, RateLimitPolicyOptions limit) =>
        options.AddPolicy(name, httpContext => RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = limit.PermitLimit,
                Window = limit.Window,
                QueueLimit = 0
            }));
}
