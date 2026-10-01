using GaussAuth.Api.DependencyInjection;
using GaussAuth.Api.Users;
using GaussAuth.Api.Applications;
using GaussAuth.Api.Memberships;
using GaussAuth.Api.Roles;
using GaussAuth.Api.Permissions;
using GaussAuth.Api.Authorization;
using GaussAuth.Api.Login;
using GaussAuth.Api.Sessions;
using GaussAuth.Api.Passwords;
using GaussAuth.Api.AuthorizationContext;
using GaussAuth.Api.Security;
using GaussAuth.Infrastructure.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);
var maximumRequestBodyBytes = builder.Configuration.GetValue("RequestLimits:MaxBodyBytes", 65_536);
if (maximumRequestBodyBytes is < 1 or > 1_048_576) throw new InvalidOperationException("Request body limit configuration is invalid.");
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = maximumRequestBodyBytes);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration, builder.Environment);
builder.Services.AddApiServices(builder.Configuration);

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseApiSecurityHeaders();
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}
app.UseRateLimiter();
app.MapGet("/health/live", () => Results.NoContent());
app.MapUsersEndpoints();
app.MapApplicationsEndpoints();
app.MapMembershipsEndpoints();
app.MapRolesEndpoints();
app.MapPermissionsEndpoints();
app.MapAuthorizationEndpoints();
app.MapLoginEndpoints();
app.MapSessionsEndpoints();
app.MapPasswordsEndpoints();
app.MapAuthorizationContextEndpoints();
app.MapSecurityEventEndpoints();

app.Run();
