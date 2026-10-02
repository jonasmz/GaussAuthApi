using GaussAuth.Application.Security;
using GaussAuth.Api.Sessions;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace GaussAuth.Api.Security;

public static class SecurityEventEndpoints
{
    public static IEndpointRouteBuilder MapSecurityEventEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/security-events", GetAsync).RequireRateLimiting("security-events");
        return app;
    }

    private static async Task<Results<Ok<SecurityEventsResponse>, ProblemHttpResult>> GetAsync(
        [AsParameters] AuditQueryRequest request, HttpContext context, SecurityEventQueryService service, CancellationToken cancellationToken)
    {
        if (!TryCreateQuery(request, out var query)) return TypedResults.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Audit query is invalid.");
        var credential = context.Request.ReadBearerCredential();
        if (credential is null) return Unauthorized(context);

        var result = await service.QueryAsync(credential, query!, cancellationToken);
        if (result.Failure == "invalid") return TypedResults.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Audit query is invalid.");
        if (result.Failure == "unauthorized") return Unauthorized(context);
        if (result.Failure == "forbidden") return TypedResults.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Audit query is not authorized.");

        context.Response.Headers.CacheControl = "no-store";
        return TypedResults.Ok(new SecurityEventsResponse(result.Items.Select(item => new SecurityEventResponse(item.Id,
            item.EventType, item.Outcome, item.OccurredAtUtc, item.UserId, item.ApplicationId, item.SessionId,
            item.ConsumerApplicationId, item.CorrelationId, item.SubjectType, item.SubjectId, item.Reason, item.ActorUserId)).ToArray(), result.NextCursor));
    }

    private static bool TryCreateQuery(AuditQueryRequest request, out SecurityEventQuery? query)
    {
        query = null;
        SecurityEventOutcome? parsedOutcome = null;
        if (request.Outcome is not null)
        {
            if (!Enum.TryParse<SecurityEventOutcome>(request.Outcome, true, out var outcome)) return false;
            parsedOutcome = outcome;
        }
        query = new SecurityEventQuery(request.FromUtc, request.ToUtc, request.EventType, parsedOutcome, request.UserId,
            request.ApplicationId, request.SessionId, request.PageSize ?? 50, request.Cursor);
        return true;
    }

    private static ProblemHttpResult Unauthorized(HttpContext context)
    {
        context.Response.Headers.WWWAuthenticate = "Bearer";
        return TypedResults.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Access is not valid.");
    }
}
