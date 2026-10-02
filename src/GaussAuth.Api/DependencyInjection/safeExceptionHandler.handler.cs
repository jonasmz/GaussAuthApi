using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace GaussAuth.Api.DependencyInjection;

public sealed class SafeExceptionHandler(
    IProblemDetailsService problemDetails,
    ILogger<SafeExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext context,
        Exception exception,
        CancellationToken cancellationToken)
    {
        logger.LogError(exception, "Unexpected API error. TraceId {TraceId}", context.TraceIdentifier);
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
