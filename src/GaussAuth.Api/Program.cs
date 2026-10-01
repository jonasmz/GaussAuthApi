using GaussAuth.Api.DependencyInjection;
using GaussAuth.Api.Users;
using GaussAuth.Api.Applications;
using GaussAuth.Api.Memberships;
using GaussAuth.Api.Roles;
using GaussAuth.Api.Permissions;
using GaussAuth.Api.Authorization;
using GaussAuth.Infrastructure.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApiServices(builder.Configuration);

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseRateLimiter();
app.MapGet("/health/live", () => Results.NoContent());
app.MapUsersEndpoints();
app.MapApplicationsEndpoints();
app.MapMembershipsEndpoints();
app.MapRolesEndpoints();
app.MapPermissionsEndpoints();
app.MapAuthorizationEndpoints();

app.Run();
