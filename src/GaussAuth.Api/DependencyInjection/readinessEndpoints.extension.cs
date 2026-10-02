using GaussAuth.Infrastructure.Health;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace GaussAuth.Api.DependencyInjection;

public static class ReadinessEndpoints
{
    public static IServiceCollection AddReadinessChecks(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddCheck<DatabaseReadinessCheck>("database-readiness")
            .AddCheck<ProfileStorageReadinessCheck>("profile-storage-readiness");
        services.AddSingleton<ReadinessResultCache>();
        return services;
    }

    public static IEndpointRouteBuilder MapReadinessEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/health/ready", WriteReadinessAsync);
        return app;
    }

    private static async Task WriteReadinessAsync(HttpContext context, HealthCheckService healthChecks, ReadinessResultCache cache)
    {
        var report = await cache.GetAsync(healthChecks, context.RequestAborted);
        context.Response.StatusCode = report.Status == HealthStatus.Healthy
            ? StatusCodes.Status204NoContent
            : StatusCodes.Status503ServiceUnavailable;
        context.Response.ContentLength = 0;
    }
}
