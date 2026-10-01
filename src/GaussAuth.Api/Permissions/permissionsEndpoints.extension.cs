using GaussAuth.Application.Permissions;
using Microsoft.AspNetCore.Mvc;

namespace GaussAuth.Api.Permissions;

public static class PermissionsEndpoints
{
    public static IEndpointRouteBuilder MapPermissionsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/applications/{applicationId:guid}/permissions", CreateAsync);
        app.MapGet("/applications/{applicationId:guid}/permissions/{permissionId:guid}", GetAsync);
        app.MapGet("/applications/{applicationId:guid}/permissions", ListAsync);
        app.MapPut("/applications/{applicationId:guid}/permissions/{permissionId:guid}/description", UpdateDescriptionAsync);
        app.MapPost("/applications/{applicationId:guid}/permissions/{permissionId:guid}/activate", ActivateAsync);
        app.MapPost("/applications/{applicationId:guid}/permissions/{permissionId:guid}/deactivate", DeactivateAsync);
        return app;
    }

    private static async Task<IResult> CreateAsync(Guid applicationId, CreatePermissionRequest request, PermissionService service, CancellationToken ct)
    {
        var result = await service.CreateAsync(applicationId, request.Code, request.Description, ct);
        return result.Permission is { } permission
            ? TypedResults.Created($"/applications/{applicationId}/permissions/{permission.Id}", PermissionResponse.FromDomain(permission))
            : Failure(result.Failure);
    }

    private static async Task<IResult> GetAsync(Guid applicationId, Guid permissionId, PermissionService service, CancellationToken ct)
        => await service.GetAsync(applicationId, permissionId, ct) is { } permission ? TypedResults.Ok(PermissionResponse.FromDomain(permission)) : NotFound();

    private static async Task<IResult> ListAsync(Guid applicationId, string? cursor, int? limit, PermissionService service, CancellationToken ct)
        => Page(await service.ListAsync(applicationId, cursor, limit, ct));

    private static async Task<IResult> UpdateDescriptionAsync(Guid applicationId, Guid permissionId, UpdatePermissionDescriptionRequest request, PermissionService service, CancellationToken ct)
        => Operation(await service.UpdateDescriptionAsync(applicationId, permissionId, request.Description, ct));

    private static async Task<IResult> ActivateAsync(Guid applicationId, Guid permissionId, PermissionService service, CancellationToken ct)
        => Operation(await service.ActivateAsync(applicationId, permissionId, ct));

    private static async Task<IResult> DeactivateAsync(Guid applicationId, Guid permissionId, PermissionService service, CancellationToken ct)
        => Operation(await service.DeactivateAsync(applicationId, permissionId, ct));

    private static IResult Page(PermissionPage page) => page.Failure switch
    {
        "application-not-found" => NotFound(),
        not null => TypedResults.BadRequest(),
        _ => TypedResults.Ok(new PermissionListResponse(page.Items.Select(PermissionResponse.FromDomain).ToArray(), page.NextCursor)),
    };

    private static IResult Operation(PermissionOperationResult result) => result.Permission is { } permission ? TypedResults.Ok(PermissionResponse.FromDomain(permission)) : Failure(result.Failure);

    private static IResult Failure(string? failure) => failure switch
    {
        "application-not-found" or "permission-not-found" => NotFound(),
        "inactive-application" or "duplicate" => Conflict(),
        _ => TypedResults.BadRequest(),
    };

    private static IResult NotFound() => TypedResults.NotFound(new ProblemDetails { Status = 404, Title = "Resource not found." });
    private static IResult Conflict() => TypedResults.Conflict(new ProblemDetails { Status = 409, Title = "Request conflicts with current state." });
}
