using GaussAuth.Application.Administration.ConsumerCredentials;
using Microsoft.AspNetCore.Mvc;

namespace GaussAuth.Api.Administration;

public static class ConsumerSecretEndpoints
{
    public static IEndpointRouteBuilder MapConsumerSecretEndpoints(this IEndpointRouteBuilder app)
    {
        const string route = "/applications/{applicationId:guid}/consumer-secret";
        app.MapPost(route, GenerateAsync).RequireGlobalAdministrator();
        app.MapPost(route + "/rotate", RotateAsync).RequireGlobalAdministrator();
        app.MapPost(route + "/retire-previous", RetirePreviousAsync).RequireGlobalAdministrator();
        app.MapGet(route, GetAsync).RequireGlobalAdministrator();
        return app;
    }

    private static async Task<IResult> GenerateAsync(Guid applicationId, HttpContext http, GenerateConsumerSecretHandler handler, CancellationToken ct) =>
        Issued(http, await handler.HandleAsync(applicationId, ct), StatusCodes.Status201Created);

    private static async Task<IResult> RotateAsync(Guid applicationId, HttpContext http, RotateConsumerSecretHandler handler, CancellationToken ct) =>
        Issued(http, await handler.HandleAsync(applicationId, ct), StatusCodes.Status200OK);

    private static async Task<IResult> RetirePreviousAsync(Guid applicationId, HttpContext http, RetirePreviousConsumerSecretHandler handler, CancellationToken ct) =>
        Metadata(http, await handler.HandleAsync(applicationId, ct));

    private static async Task<IResult> GetAsync(Guid applicationId, HttpContext http, GetConsumerSecretMetadataHandler handler, CancellationToken ct) =>
        Metadata(http, await handler.HandleAsync(new GetConsumerSecretMetadataQuery(applicationId), ct));

    private static IResult Issued(HttpContext http, ConsumerSecretOperationResult result, int successStatus)
    {
        http.Response.Headers.CacheControl = "no-store";
        if (!result.IsSuccess) return Failure(result);
        return TypedResults.Json(ConsumerSecretIssuanceResponse.FromResult(result.Issuance!), statusCode: successStatus);
    }

    private static IResult Metadata(HttpContext http, ConsumerSecretOperationResult result)
    {
        http.Response.Headers.CacheControl = "no-store";
        return result.IsSuccess ? TypedResults.Ok(ConsumerSecretMetadataResponse.FromResult(result.Metadata!)) : Failure(result);
    }

    private static IResult Failure(ConsumerSecretOperationResult result) => result.Failure == "application-not-found"
        ? TypedResults.NotFound(new ProblemDetails { Status = 404, Title = "Resource not found." })
        : TypedResults.Conflict(new ProblemDetails { Status = 409, Title = "Request conflicts with current state." });
}
