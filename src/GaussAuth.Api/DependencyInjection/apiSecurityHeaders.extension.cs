using System.Diagnostics;

namespace GaussAuth.Api.DependencyInjection;

public static class ApiSecurityHeaders
{
    public static IApplicationBuilder UseApiSecurityHeaders(this IApplicationBuilder app)
    {
        app.Use(async (context, next) =>
        {
            context.Response.Headers.TryAdd("X-Content-Type-Options", "nosniff");
            context.Response.Headers.TryAdd("Referrer-Policy", "no-referrer");
            var correlationId = Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;
            context.Response.Headers.TryAdd("X-Correlation-Id", correlationId);
            await next();
        });
        return app;
    }
}
