using GaussAuth.Application.Roles;
using GaussAuth.Api.Administration;
using GaussAuth.Application.Administration.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GaussAuth.Api.Roles;

public static class RolesEndpoints
{
    public static IEndpointRouteBuilder MapRolesEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/applications/{applicationId:guid}/roles", CreateAsync).RequireApplicationAdministrator(AdministrativePermissionCatalog.RolesManage);
        app.MapGet("/applications/{applicationId:guid}/roles/{roleId:guid}", GetAsync).RequireApplicationAdministrator(AdministrativePermissionCatalog.RolesRead);
        app.MapGet("/applications/{applicationId:guid}/roles", ListAsync).RequireApplicationAdministrator(AdministrativePermissionCatalog.RolesRead);
        app.MapPut("/applications/{applicationId:guid}/roles/{roleId:guid}/description", UpdateDescriptionAsync).RequireApplicationAdministrator(AdministrativePermissionCatalog.RolesManage);
        app.MapPost("/applications/{applicationId:guid}/roles/{roleId:guid}/activate", ActivateAsync).RequireApplicationAdministrator(AdministrativePermissionCatalog.RolesManage);
        app.MapPost("/applications/{applicationId:guid}/roles/{roleId:guid}/deactivate", DeactivateAsync).RequireApplicationAdministrator(AdministrativePermissionCatalog.RolesManage);
        return app;
    }

    private static async Task<IResult> CreateAsync(Guid applicationId, CreateRoleRequest request, RoleService service, CancellationToken ct)
    {
        var result = await service.CreateAsync(applicationId, request.Name, request.Description, ct);
        return result.Role is { } role
            ? TypedResults.Created($"/admin/applications/{applicationId}/roles/{role.Id}", RoleResponse.FromDomain(role))
            : Failure(result.Failure);
    }

    private static async Task<IResult> GetAsync(Guid applicationId, Guid roleId, RoleService service, CancellationToken ct)
        => await service.GetAsync(applicationId, roleId, ct) is { } role ? TypedResults.Ok(RoleResponse.FromDomain(role)) : NotFound();

    private static async Task<IResult> ListAsync(Guid applicationId, string? cursor, int? limit, RoleService service, CancellationToken ct)
        => Page(await service.ListAsync(applicationId, cursor, limit, ct));

    private static async Task<IResult> UpdateDescriptionAsync(Guid applicationId, Guid roleId, UpdateRoleDescriptionRequest request, RoleService service, CancellationToken ct)
        => Operation(await service.UpdateDescriptionAsync(applicationId, roleId, request.Description, ct));

    private static async Task<IResult> ActivateAsync(Guid applicationId, Guid roleId, RoleService service, CancellationToken ct)
        => Operation(await service.ActivateAsync(applicationId, roleId, ct));

    private static async Task<IResult> DeactivateAsync(Guid applicationId, Guid roleId, RoleService service, CancellationToken ct)
        => Operation(await service.DeactivateAsync(applicationId, roleId, ct));

    private static IResult Page(RolePage page) => page.Failure switch
    {
        "application-not-found" => NotFound(),
        not null => TypedResults.BadRequest(),
        _ => TypedResults.Ok(new RoleListResponse(page.Items.Select(RoleResponse.FromDomain).ToArray(), page.NextCursor)),
    };

    private static IResult Operation(RoleOperationResult result) => result.Role is { } role ? TypedResults.Ok(RoleResponse.FromDomain(role)) : Failure(result.Failure);

    private static IResult Failure(string? failure) => failure switch
    {
        "application-not-found" or "role-not-found" => NotFound(),
        "inactive-application" or "duplicate" or "platform-permission" => Conflict(),
        _ => TypedResults.BadRequest(),
    };

    private static IResult NotFound() => TypedResults.NotFound(new ProblemDetails { Status = 404, Title = "Resource not found." });
    private static IResult Conflict() => TypedResults.Conflict(new ProblemDetails { Status = 409, Title = "Request conflicts with current state." });
}
