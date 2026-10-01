using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace GaussAuth.Api.DependencyInjection;

public static class ApiServiceCollectionExtensions
{
    public static IServiceCollection AddApiServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddProblemDetails();
        services.AddExceptionHandler<SafeExceptionHandler>();

        var userCreationPermitLimit = configuration.GetValue("RateLimiting:UserCreation:PermitLimit", 5);
        var userCreationWindow = TimeSpan.FromSeconds(configuration.GetValue("RateLimiting:UserCreation:WindowSeconds", 60));
        var loginPermitLimit = configuration.GetValue("RateLimiting:Login:PermitLimit", 5);
        var loginWindow = TimeSpan.FromSeconds(configuration.GetValue("RateLimiting:Login:WindowSeconds", 60));
        var signingKeysPermitLimit = configuration.GetValue("RateLimiting:SigningKeys:PermitLimit", 60);
        var signingKeysWindow = TimeSpan.FromSeconds(configuration.GetValue("RateLimiting:SigningKeys:WindowSeconds", 60));
        var sessionCredentialsPermitLimit = configuration.GetValue("RateLimiting:SessionCredentials:PermitLimit", 600);
        var sessionCredentialsWindow = TimeSpan.FromSeconds(configuration.GetValue("RateLimiting:SessionCredentials:WindowSeconds", 60));

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

            options.AddPolicy("user-creation", httpContext => RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = userCreationPermitLimit,
                    Window = userCreationWindow,
                    QueueLimit = 0
                }));

            options.AddPolicy("login", httpContext => RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = loginPermitLimit,
                    Window = loginWindow,
                    QueueLimit = 0
                }));

            options.AddPolicy("signing-keys", httpContext => RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                factory: _ => new FixedWindowRateLimiterOptions { PermitLimit = signingKeysPermitLimit, Window = signingKeysWindow, QueueLimit = 0 }));

            options.AddPolicy("session-credentials", httpContext => RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                factory: _ => new FixedWindowRateLimiterOptions { PermitLimit = sessionCredentialsPermitLimit, Window = sessionCredentialsWindow, QueueLimit = 0 }));
        });

        return services;
    }
}
