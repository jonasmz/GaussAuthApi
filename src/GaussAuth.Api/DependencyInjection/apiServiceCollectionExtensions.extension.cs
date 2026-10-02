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

        var userCreationPermitLimit = configuration.GetValue("RateLimiting:UserCreation:PermitLimit", 5);
        var userCreationWindow = TimeSpan.FromSeconds(configuration.GetValue("RateLimiting:UserCreation:WindowSeconds", 60));
        var loginPermitLimit = configuration.GetValue("RateLimiting:Login:PermitLimit", 5);
        var loginWindow = TimeSpan.FromSeconds(configuration.GetValue("RateLimiting:Login:WindowSeconds", 60));
        var signingKeysPermitLimit = configuration.GetValue("RateLimiting:SigningKeys:PermitLimit", 60);
        var signingKeysWindow = TimeSpan.FromSeconds(configuration.GetValue("RateLimiting:SigningKeys:WindowSeconds", 60));
        var sessionCredentialsPermitLimit = configuration.GetValue("RateLimiting:SessionCredentials:PermitLimit", 600);
        var sessionCredentialsWindow = TimeSpan.FromSeconds(configuration.GetValue("RateLimiting:SessionCredentials:WindowSeconds", 60));
        var passwordRecoveryPermitLimit = configuration.GetValue("RateLimiting:PasswordRecovery:PermitLimit", 5);
        var passwordRecoveryWindow = TimeSpan.FromSeconds(configuration.GetValue("RateLimiting:PasswordRecovery:WindowSeconds", 60));
        var passwordResetPermitLimit = configuration.GetValue("RateLimiting:PasswordReset:PermitLimit", 5);
        var passwordResetWindow = TimeSpan.FromSeconds(configuration.GetValue("RateLimiting:PasswordReset:WindowSeconds", 60));
        var authorizationContextPermitLimit = configuration.GetValue("RateLimiting:AuthorizationContext:PermitLimit", 600);
        var authorizationContextWindow = TimeSpan.FromSeconds(configuration.GetValue("RateLimiting:AuthorizationContext:WindowSeconds", 60));
        var securityEventsPermitLimit = configuration.GetValue("RateLimiting:SecurityEvents:PermitLimit", 60);
        var securityEventsWindow = TimeSpan.FromSeconds(configuration.GetValue("RateLimiting:SecurityEvents:WindowSeconds", 60));

        var administrationPermitLimit = configuration.GetValue("RateLimiting:Administration:PermitLimit", 120);
        var administrationWindow = TimeSpan.FromSeconds(configuration.GetValue("RateLimiting:Administration:WindowSeconds", 60));
        if (administrationPermitLimit < 1 || administrationWindow <= TimeSpan.Zero) throw new InvalidOperationException("Administration rate limit configuration is invalid.");
        var profileImageWritePermitLimit = configuration.GetValue("RateLimiting:ProfileImageWrite:PermitLimit", 10);
        var profileImageWriteWindow = TimeSpan.FromSeconds(configuration.GetValue("RateLimiting:ProfileImageWrite:WindowSeconds", 60));

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

            options.AddPolicy("password-recovery", httpContext => RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                factory: _ => new FixedWindowRateLimiterOptions { PermitLimit = passwordRecoveryPermitLimit, Window = passwordRecoveryWindow, QueueLimit = 0 }));
            options.AddPolicy("password-reset", httpContext => RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                factory: _ => new FixedWindowRateLimiterOptions { PermitLimit = passwordResetPermitLimit, Window = passwordResetWindow, QueueLimit = 0 }));
            options.AddPolicy("authorization-context", httpContext => RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                factory: _ => new FixedWindowRateLimiterOptions { PermitLimit = authorizationContextPermitLimit, Window = authorizationContextWindow, QueueLimit = 0 }));
            options.AddPolicy("profile-image-write", httpContext => RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                factory: _ => new FixedWindowRateLimiterOptions { PermitLimit = profileImageWritePermitLimit, Window = profileImageWriteWindow, QueueLimit = 0 }));
            options.AddPolicy("administration", httpContext => RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                factory: _ => new FixedWindowRateLimiterOptions { PermitLimit = administrationPermitLimit, Window = administrationWindow, QueueLimit = 0 }));
            options.AddPolicy("security-events", httpContext => RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                factory: _ => new FixedWindowRateLimiterOptions { PermitLimit = securityEventsPermitLimit, Window = securityEventsWindow, QueueLimit = 0 }));
        });

        return services;
    }
}
