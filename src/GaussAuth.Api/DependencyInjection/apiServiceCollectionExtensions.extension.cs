using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace GaussAuth.Api.DependencyInjection;

public static class ApiServiceCollectionExtensions
{
    public static IServiceCollection AddApiServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddProblemDetails();
        services.AddExceptionHandler<SafeExceptionHandler>();

        var permitLimit = configuration.GetValue("RateLimiting:UserCreation:PermitLimit", 5);
        var window = TimeSpan.FromSeconds(configuration.GetValue("RateLimiting:UserCreation:WindowSeconds", 60));

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = (context, cancellationToken) =>
            {
                context.HttpContext.Response.Headers.RetryAfter = ((int)window.TotalSeconds).ToString();
                return ValueTask.CompletedTask;
            };

            options.AddPolicy("user-creation", httpContext => RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = permitLimit,
                    Window = window,
                    QueueLimit = 0
                }));
        });

        return services;
    }
}
