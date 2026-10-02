using GaussAuth.Application.Administration.Authorization;
using GaussAuth.Application.Administration.Sessions;
using Microsoft.AspNetCore.Mvc;

namespace GaussAuth.Api.Administration;

public static class AdministrativeSessionEndpoints
{
    public static IEndpointRouteBuilder MapAdministrativeSessionEndpoints(this IEndpointRouteBuilder app)
    {
        const string sessions = "/applications/{applicationId:guid}/sessions";
        app.MapGet(sessions, ListApplicationAsync).RequireApplicationAdministrator(AdministrativePermissionCatalog.SessionsRead);
        app.MapGet(sessions + "/{sessionId:guid}", GetAsync).RequireApplicationAdministrator(AdministrativePermissionCatalog.SessionsRead);
        app.MapPost(sessions + "/{sessionId:guid}/revoke", RevokeAsync).RequireApplicationAdministrator(AdministrativePermissionCatalog.SessionsRevoke);
        app.MapPost(sessions + "/revoke", RevokeAllAsync).RequireApplicationAdministrator(AdministrativePermissionCatalog.SessionsRevoke);
        app.MapPost("/applications/{applicationId:guid}/users/{userId:guid}/sessions/revoke", RevokeUserInApplicationAsync)
            .RequireApplicationAdministrator(AdministrativePermissionCatalog.SessionsRevoke);
        app.MapGet("/users/{userId:guid}/sessions", ListUserAsync).RequireGlobalAdministrator();
        app.MapPost("/users/{userId:guid}/sessions/revoke", RevokeUserEverywhereAsync).RequireGlobalAdministrator();
        return app;
    }

    private static async Task<IResult> ListApplicationAsync(Guid applicationId, Guid? userId, string? state, string? cursor, int? limit, AdministrativeSessionService service, CancellationToken ct) =>
        Page(await service.ListAsync(applicationId, userId, state, cursor, limit, ct));

    private static async Task<IResult> ListUserAsync(Guid userId, string? state, string? cursor, int? limit, AdministrativeSessionService service, CancellationToken ct) =>
        Page(await service.ListAsync(null, userId, state, cursor, limit, ct));

    private static async Task<IResult> GetAsync(Guid applicationId, Guid sessionId, AdministrativeSessionService service, CancellationToken ct) =>
        One(await service.GetAsync(applicationId, sessionId, ct));

    private static async Task<IResult> RevokeAsync(Guid applicationId, Guid sessionId, AdministrativeSessionService service, CancellationToken ct) =>
        One(await service.RevokeAsync(applicationId, sessionId, ct));

    private static async Task<IResult> RevokeUserInApplicationAsync(Guid applicationId, Guid userId, AdministrativeSessionService service, CancellationToken ct) =>
        Bulk(await service.RevokeUserInApplicationAsync(applicationId, userId, ct));

    private static async Task<IResult> RevokeAllAsync(Guid applicationId, AdministrativeSessionService service, CancellationToken ct) =>
        Bulk(await service.RevokeAllInApplicationAsync(applicationId, ct));

    private static async Task<IResult> RevokeUserEverywhereAsync(Guid userId, AdministrativeSessionService service, CancellationToken ct) =>
        Bulk(await service.RevokeUserEverywhereAsync(userId, ct));

    private static IResult Page(AdministrativeSessionPage page) => page.Failure switch
    {
        "not-found" => NotFound(),
        not null => TypedResults.BadRequest(),
        _ => TypedResults.Ok(new AdminSessionListResponse(page.Items.Select(AdminSessionResponse.FromResult).ToArray(), page.NextCursor)),
    };

    private static IResult One(AdministrativeSessionOperationResult result) =>
        result.Session is { } session ? TypedResults.Ok(AdminSessionResponse.FromResult(session)) : NotFound();

    private static IResult Bulk(BulkSessionRevocationResult result) =>
        result.IsNotFound ? NotFound() : TypedResults.Ok(new BulkSessionRevocationResponse(result.Revoked, result.HasMore));

    private static IResult NotFound() => TypedResults.NotFound(new ProblemDetails { Status = 404, Title = "Resource not found." });
}
