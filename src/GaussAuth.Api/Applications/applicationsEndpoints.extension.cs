using System.ComponentModel.DataAnnotations;
using GaussAuth.Application.Applications;
using Microsoft.AspNetCore.Mvc;

namespace GaussAuth.Api.Applications;

public static class ApplicationsEndpoints
{
    public static IEndpointRouteBuilder MapApplicationsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/applications", CreateAsync); app.MapGet("/applications", ListAsync); app.MapGet("/applications/by-code/{code}", GetByCodeAsync); app.MapGet("/applications/{id:guid}", GetAsync); app.MapPost("/applications/{id:guid}/activate", ActivateAsync); app.MapPost("/applications/{id:guid}/deactivate", DeactivateAsync); return app;
    }
    private static async Task<IResult> CreateAsync(CreateApplicationRequest request, ApplicationService service, CancellationToken ct)
    {
        if (!Validator.TryValidateObject(request, new ValidationContext(request), null, true)) return TypedResults.BadRequest();
        var result = await service.CreateAsync(request.Code, request.Name, ct);
        if (result.Application is not null) return TypedResults.Created($"/applications/{result.Application.Id}", ApplicationResponse.FromDomain(result.Application));
        return result.IsDuplicate ? TypedResults.Conflict(new ProblemDetails { Status = 409, Title = "Application code already exists." }) : TypedResults.BadRequest();
    }
    private static async Task<IResult> GetAsync(Guid id, ApplicationService service, CancellationToken ct) => await service.GetByIdAsync(id, ct) is { } item ? TypedResults.Ok(ApplicationResponse.FromDomain(item)) : NotFound();
    private static async Task<IResult> GetByCodeAsync(string code, ApplicationService service, CancellationToken ct) => await service.GetByCodeAsync(code, ct) is { } item ? TypedResults.Ok(ApplicationResponse.FromDomain(item)) : NotFound();
    private static async Task<IResult> ListAsync(string? cursor, int? limit, ApplicationService service, CancellationToken ct) { var page = await service.ListAsync(cursor, limit, ct); return page.IsInvalid ? TypedResults.BadRequest() : TypedResults.Ok(new ApplicationListResponse(page.Items.Select(ApplicationResponse.FromDomain).ToArray(), page.NextCursor)); }
    private static async Task<IResult> ActivateAsync(Guid id, ApplicationService service, CancellationToken ct) => await service.ActivateAsync(id, ct) is { } item ? TypedResults.Ok(ApplicationResponse.FromDomain(item)) : NotFound();
    private static async Task<IResult> DeactivateAsync(Guid id, ApplicationService service, CancellationToken ct) => await service.DeactivateAsync(id, ct) is { } item ? TypedResults.Ok(ApplicationResponse.FromDomain(item)) : NotFound();
    private static IResult NotFound() => TypedResults.NotFound(new ProblemDetails { Status = 404, Title = "Application not found." });
}
