using GaussAuth.Api.Commands;
using GaussAuth.Api.DependencyInjection;
using GaussAuth.Api.Startup;
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
using GaussAuth.Infrastructure.Configuration;
using GaussAuth.Infrastructure.DependencyInjection;
using Microsoft.Extensions.Options;

// One-off operator subcommands (migrate, bootstrap-admin) run in their own minimal host and exit.
var commandExitCode = await CommandLineCommands.TryRunAsync(args);
if (commandExitCode is { } exitCode) return exitCode;

try
{
    var builder = WebApplication.CreateBuilder(args);
    var maximumRequestBodyBytes = builder.Configuration.GetBoundedInt32("RequestLimits:MaxBodyBytes", 65_536, 1, 1_048_576);
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
    app.MapReadinessEndpoints();
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

    await app.RunAsync();
    return 0;
}
catch (Exception exception) when (exception is StartupConfigurationException or OptionsValidationException)
{
    // Startup configuration failures: one value-free structured Critical log, non-zero exit, no stack trace.
    return StartupFailureReporter.Report(exception);
}
