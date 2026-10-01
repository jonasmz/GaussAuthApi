using System.ComponentModel.DataAnnotations;
using GaussAuth.Application.Applications;
using GaussAuth.Api.Administration;
using Microsoft.AspNetCore.Mvc;

namespace GaussAuth.Api.Applications;

public static class ApplicationsEndpoints
{
    public static IEndpointRouteBuilder MapApplicationsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/applications", CreateAsync).RequireGlobalAdministrator(); app.MapGet("/applications", ListAsync).RequireGlobalAdministrator(); app.MapGet("/applications/by-code/{code}", GetByCodeAsync).RequireGlobalAdministrator(); app.MapGet("/applications/{applicationId:guid}", GetAsync).RequireGlobalAdministrator(); app.MapPost("/applications/{applicationId:guid}/activate", ActivateAsync).RequireGlobalAdministrator(); app.MapPost("/applications/{applicationId:guid}/deactivate", DeactivateAsync).RequireGlobalAdministrator(); return app;
    }
    private static async Task<IResult> CreateAsync(CreateApplicationRequest request, ApplicationService service, CancellationToken ct)
    {
        if (!Validator.TryValidateObject(request, new ValidationContext(request), null, true)) return TypedResults.BadRequest();
        var result = await service.CreateAsync(request.Code, request.Name, ct);
        if (result.Application is not null) return TypedResults.Created($"/admin/applications/{result.Application.Id}", ApplicationResponse.FromDomain(result.Application));
        return result.IsDuplicate ? TypedResults.Conflict(new ProblemDetails { Status = 409, Title = "Application code already exists." }) : TypedResults.BadRequest();
    }
    private static async Task<IResult> GetAsync(Guid applicationId, ApplicationService service, CancellationToken ct) => await service.GetByIdAsync(applicationId, ct) is { } item ? TypedResults.Ok(ApplicationResponse.FromDomain(item)) : NotFound();
    private static async Task<IResult> GetByCodeAsync(string code, ApplicationService service, CancellationToken ct) => await service.GetByCodeAsync(code, ct) is { } item ? TypedResults.Ok(ApplicationResponse.FromDomain(item)) : NotFound();
    private static async Task<IResult> ListAsync(string? cursor, int? limit, ApplicationService service, CancellationToken ct) { var page = await service.ListAsync(cursor, limit, ct); return page.IsInvalid ? TypedResults.BadRequest() : TypedResults.Ok(new ApplicationListResponse(page.Items.Select(ApplicationResponse.FromDomain).ToArray(), page.NextCursor)); }
    private static async Task<IResult> ActivateAsync(Guid applicationId, ApplicationService service, CancellationToken ct) => await service.ActivateAsync(applicationId, ct) is { } item ? TypedResults.Ok(ApplicationResponse.FromDomain(item)) : NotFound();
    private static async Task<IResult> DeactivateAsync(Guid applicationId, ApplicationService service, CancellationToken ct) => await service.DeactivateAsync(applicationId, ct) is { } item ? TypedResults.Ok(ApplicationResponse.FromDomain(item)) : NotFound();
    private static IResult NotFound() => TypedResults.NotFound(new ProblemDetails { Status = 404, Title = "Application not found." });
}
