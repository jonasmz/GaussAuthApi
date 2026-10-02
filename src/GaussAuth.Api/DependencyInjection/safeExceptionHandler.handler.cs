using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using GaussAuth.Application.Security;
using GaussAuth.Application.Security.Ports;
using System.Diagnostics;

namespace GaussAuth.Api.DependencyInjection;

public sealed class SafeExceptionHandler(
    IProblemDetailsService problemDetails,
    ILogger<SafeExceptionHandler> logger,
    IServiceScopeFactory scopes) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext context,
        Exception exception,
        CancellationToken cancellationToken)
    {
        // Exception text can contain request values, SQL, or paths. Keep the operational log value-free.
        var correlationId = Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;
        context.Response.Headers.TryAdd("X-Correlation-Id", correlationId);
        logger.LogError("Unexpected API error. TraceId {TraceId}", correlationId);
        await using (var scope = scopes.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<ISecurityEventRecorder>().RecordAsync(
                new SecurityEventDraft(
                    scope.ServiceProvider.GetRequiredService<SecurityEventCatalog>().Get(SecurityEventType.UnexpectedApplicationFailure),
                    null, null, null,
                    CorrelationId: correlationId), cancellationToken);
        }
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;

        var written = await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "An unexpected error occurred."
            }
        });

        if (!written)
        {
            context.Response.ContentType = "text/plain";
            await context.Response.WriteAsync("An unexpected error occurred.", cancellationToken);
        }

        return true;
    }
}
