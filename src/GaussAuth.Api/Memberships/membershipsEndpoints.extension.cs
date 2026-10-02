using GaussAuth.Application.Memberships;
using GaussAuth.Api.Administration;
using GaussAuth.Application.Administration.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GaussAuth.Api.Memberships;

public static class MembershipsEndpoints
{
    public static IEndpointRouteBuilder MapMembershipsEndpoints(this IEndpointRouteBuilder app)
    {
        const string a = "/applications/{applicationId:guid}/memberships";
        app.MapPost(a, CreateAsync).RequireApplicationAdministrator(AdministrativePermissionCatalog.MembershipsManage);
        app.MapGet(a + "/{userId:guid}", GetAsync).RequireApplicationAdministrator(AdministrativePermissionCatalog.MembershipsRead);
        app.MapPost(a + "/{userId:guid}/activate", ActivateAsync).RequireApplicationAdministrator(AdministrativePermissionCatalog.MembershipsManage);
        app.MapPost(a + "/{userId:guid}/deactivate", DeactivateAsync).RequireApplicationAdministrator(AdministrativePermissionCatalog.MembershipsManage);
        app.MapGet(a, ListApplicationAsync).RequireApplicationAdministrator(AdministrativePermissionCatalog.MembershipsRead);
        app.MapGet("/users/{userId:guid}/memberships", ListUserAsync).RequireGlobalAdministrator();
        return app;
    }
    private static async Task<IResult> CreateAsync(Guid applicationId, CreateMembershipRequest request, MembershipService service, CancellationToken ct)
    { var result = await service.CreateAsync(request.UserId, applicationId, ct); if (result.Membership is not null) return TypedResults.Created($"/admin/applications/{applicationId}/memberships/{request.UserId}", ApplicationMembershipResponse.FromDomain(result.Membership)); return result.Failure is "user-not-found" or "application-not-found" ? NotFound(result.Failure) : result.Failure is "duplicate" or "inactive-application" ? TypedResults.Conflict(new ProblemDetails { Status = 409, Title = "Membership creation conflicts with current state." }) : TypedResults.BadRequest(); }
    private static async Task<IResult> GetAsync(Guid applicationId, Guid userId, MembershipService service, CancellationToken ct) => await service.GetAsync(userId, applicationId, ct) is { } value ? TypedResults.Ok(ApplicationMembershipResponse.FromDomain(value)) : NotFound("membership-not-found");
    private static async Task<IResult> ActivateAsync(Guid applicationId, Guid userId, MembershipService service, CancellationToken ct) => Operation(await service.ActivateAsync(userId, applicationId, ct));
    private static async Task<IResult> DeactivateAsync(Guid applicationId, Guid userId, MembershipService service, CancellationToken ct) => Operation(await service.DeactivateAsync(userId, applicationId, ct));
    private static async Task<IResult> ListApplicationAsync(Guid applicationId, string? cursor, int? limit, MembershipService service, CancellationToken ct) => Page(await service.ListByApplicationAsync(applicationId, cursor, limit, ct));
    private static async Task<IResult> ListUserAsync(Guid userId, string? cursor, int? limit, MembershipService service, CancellationToken ct) => Page(await service.ListByUserAsync(userId, cursor, limit, ct));
    private static IResult Page(MembershipPage page) => page.Failure is "user-not-found" or "application-not-found" ? NotFound(page.Failure) : page.Failure is not null ? TypedResults.BadRequest() : TypedResults.Ok(new ApplicationMembershipListResponse(page.Items.Select(ApplicationMembershipResponse.FromDomain).ToArray(), page.NextCursor));
    private static IResult Operation(MembershipOperationResult result) => result.Membership is { } membership ? TypedResults.Ok(ApplicationMembershipResponse.FromDomain(membership)) : result.Failure is "membership-not-found" or "user-not-found" or "application-not-found" ? NotFound(result.Failure) : result.Failure is "inactive-user" or "inactive-application" ? TypedResults.Conflict(new ProblemDetails { Status = 409, Title = "Membership activation conflicts with current state." }) : TypedResults.BadRequest();
    private static IResult NotFound(string _) => TypedResults.NotFound(new ProblemDetails { Status = 404, Title = "Resource not found." });
}
