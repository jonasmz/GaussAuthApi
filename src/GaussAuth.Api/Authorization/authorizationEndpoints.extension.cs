using GaussAuth.Application.Administration.AuthorizationView;
using GaussAuth.Application.Authorization;
using GaussAuth.Domain.Authorization;
using GaussAuth.Api.Administration;
using GaussAuth.Application.Administration.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GaussAuth.Api.Authorization;

public static class AuthorizationEndpoints
{
    public static IEndpointRouteBuilder MapAuthorizationEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/applications/{applicationId:guid}/roles/{roleId:guid}/permissions/{permissionId:guid}", AssignRolePermissionAsync).RequireApplicationAdministrator(AdministrativePermissionCatalog.PermissionsManage);
        app.MapPost("/applications/{applicationId:guid}/roles/{roleId:guid}/permissions/{permissionId:guid}/remove", RemoveRolePermissionAsync).RequireApplicationAdministrator(AdministrativePermissionCatalog.PermissionsManage);
        app.MapGet("/applications/{applicationId:guid}/roles/{roleId:guid}/permissions", ListRolePermissionsAsync).RequireApplicationAdministrator(AdministrativePermissionCatalog.PermissionsRead);
        app.MapPost("/applications/{applicationId:guid}/users/{userId:guid}/roles/{roleId:guid}", AssignUserRoleAsync).RequireApplicationAdministrator(AdministrativePermissionCatalog.RolesManage);
        app.MapPost("/applications/{applicationId:guid}/users/{userId:guid}/roles/{roleId:guid}/remove", RemoveUserRoleAsync).RequireApplicationAdministrator(AdministrativePermissionCatalog.RolesManage);
        app.MapGet("/applications/{applicationId:guid}/users/{userId:guid}/roles", ListUserRolesAsync).RequireApplicationAdministrator(AdministrativePermissionCatalog.RolesRead);
        app.MapGet("/applications/{applicationId:guid}/users/{userId:guid}/effective-permissions", GetEffectivePermissionsAsync).RequireApplicationAdministrator(AdministrativePermissionCatalog.RolesRead);
        app.MapGet("/applications/{applicationId:guid}/users/{userId:guid}/authorization", GetAuthorizationViewAsync).RequireApplicationAdministrator(AdministrativePermissionCatalog.RolesRead);
        return app;
    }

    private static async Task<IResult> AssignRolePermissionAsync(Guid applicationId, Guid roleId, Guid permissionId, RolePermissionService service, CancellationToken ct)
    {
        var result = await service.AssignAsync(applicationId, roleId, permissionId, ct);
        if (result.RolePermission is { } rolePermission)
            return result.Created
                ? TypedResults.Created($"/admin/applications/{applicationId}/roles/{roleId}/permissions/{permissionId}", RolePermissionResponse.FromDomain(rolePermission))
                : TypedResults.Ok(RolePermissionResponse.FromDomain(rolePermission));
        return Failure(result.Failure);
    }

    private static async Task<IResult> RemoveRolePermissionAsync(Guid applicationId, Guid roleId, Guid permissionId, RolePermissionService service, CancellationToken ct)
    {
        var result = await service.RemoveAsync(applicationId, roleId, permissionId, ct);
        return result.RolePermission is { } rolePermission ? TypedResults.Ok(RolePermissionResponse.FromDomain(rolePermission)) : Failure(result.Failure);
    }

    private static async Task<IResult> ListRolePermissionsAsync(Guid applicationId, Guid roleId, string? cursor, int? limit, RolePermissionService service, CancellationToken ct)
        => RolePermissionsPage(await service.ListByRoleAsync(applicationId, roleId, cursor, limit, ct));

    private static async Task<IResult> AssignUserRoleAsync(Guid applicationId, Guid userId, Guid roleId, UserRoleService service, CancellationToken ct)
    {
        var result = await service.AssignAsync(applicationId, userId, roleId, ct);
        if (result.UserRole is { } userRole)
            return result.Created
                ? TypedResults.Created($"/admin/applications/{applicationId}/users/{userId}/roles/{roleId}", UserRoleResponse.FromDomain(userRole))
                : TypedResults.Ok(UserRoleResponse.FromDomain(userRole));
        return Failure(result.Failure);
    }

    private static async Task<IResult> RemoveUserRoleAsync(Guid applicationId, Guid userId, Guid roleId, UserRoleService service, CancellationToken ct)
    {
        var result = await service.RemoveAsync(applicationId, userId, roleId, ct);
        return result.UserRole is { } userRole ? TypedResults.Ok(UserRoleResponse.FromDomain(userRole)) : Failure(result.Failure);
    }

    private static async Task<IResult> ListUserRolesAsync(Guid applicationId, Guid userId, string? cursor, int? limit, UserRoleService service, CancellationToken ct)
        => UserRolesPage(await service.ListAsync(applicationId, userId, cursor, limit, ct));

    private static async Task<IResult> GetEffectivePermissionsAsync(Guid applicationId, Guid userId, EffectivePermissionService service, CancellationToken ct)
    {
        var page = await service.GetAsync(applicationId, userId, ct);
        return page.Failure switch
        {
            "user-not-found" or "application-not-found" => NotFound(),
            not null => TypedResults.BadRequest(),
            _ => TypedResults.Ok(new EffectivePermissionListResponse(page.Items.Select(EffectivePermissionResponse.FromDomain).ToArray())),
        };
    }

    private static async Task<IResult> GetAuthorizationViewAsync(Guid applicationId, Guid userId, GetAuthorizationViewHandler handler, CancellationToken ct)
    {
        var result = await handler.HandleAsync(new GetAuthorizationViewQuery(applicationId, userId), ct);
        return result.View is { } view ? TypedResults.Ok(AuthorizationViewResponse.FromDomain(view)) : NotFound();
    }

    private static IResult RolePermissionsPage(AuthorizationPage<RolePermission> page) => page.Failure switch
    {
        "role-not-found" => NotFound(),
        not null => TypedResults.BadRequest(),
        _ => TypedResults.Ok(new RolePermissionListResponse(page.Items.Select(RolePermissionResponse.FromDomain).ToArray(), page.NextCursor)),
    };

    private static IResult UserRolesPage(AuthorizationPage<UserRole> page) => page.Failure switch
    {
        "user-not-found" or "application-not-found" => NotFound(),
        not null => TypedResults.BadRequest(),
        _ => TypedResults.Ok(new UserRoleListResponse(page.Items.Select(UserRoleResponse.FromDomain).ToArray(), page.NextCursor)),
    };

    private static IResult Failure(string? failure) => failure switch
    {
        "application-not-found" or "role-not-found" or "permission-not-found" or "user-not-found" or "membership-not-found" or "relationship-not-found" => NotFound(),
        "inactive-application" or "inactive-role" or "inactive-permission" or "inactive-user" or "inactive-membership" or "duplicate" or "cross-application-mismatch" => Conflict(),
        _ => TypedResults.BadRequest(),
    };

    private static IResult NotFound() => TypedResults.NotFound(new ProblemDetails { Status = 404, Title = "Resource not found." });
    private static IResult Conflict() => TypedResults.Conflict(new ProblemDetails { Status = 409, Title = "Request conflicts with current state." });
}
