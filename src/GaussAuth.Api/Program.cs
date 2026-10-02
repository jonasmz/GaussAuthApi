using GaussAuth.Api.DependencyInjection;
using GaussAuth.Api.Administration;
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
using GaussAuth.Api.Profiles;
using GaussAuth.Application.Profiles.Avatars;
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
var admin = app.MapGroup("/admin").RequireRateLimiting("administration");
admin.MapUsersEndpoints();
admin.MapApplicationsEndpoints();
admin.MapMembershipsEndpoints();
admin.MapRolesEndpoints();
admin.MapPermissionsEndpoints();
admin.MapAuthorizationEndpoints();
admin.MapAdministrativeSessionEndpoints();
admin.MapConsumerSecretEndpoints();
app.MapLoginEndpoints();
app.MapSessionsEndpoints();
app.MapPasswordsEndpoints();
app.MapAuthorizationContextEndpoints();
app.MapSecurityEventEndpoints();
app.MapProfileAvatarEndpoints(app.Services.GetRequiredService<ProfileImageLimits>());

app.Run();
