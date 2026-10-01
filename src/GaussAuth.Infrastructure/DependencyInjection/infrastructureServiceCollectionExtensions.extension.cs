using GaussAuth.Application.Users.Ports;
using GaussAuth.Application.Applications.Ports;
using GaussAuth.Application.Memberships.Ports;
using GaussAuth.Application.Roles.Ports;
using GaussAuth.Application.Permissions.Ports;
using GaussAuth.Application.Authorization.Ports;
using GaussAuth.Application.Login.Ports;
using GaussAuth.Application.Security.Ports;
using GaussAuth.Application.Sessions;
using GaussAuth.Application.Sessions.Ports;
using GaussAuth.Infrastructure.Sessions;
using Microsoft.Extensions.Hosting;
using GaussAuth.Infrastructure.Identity;
using GaussAuth.Infrastructure.Passwords;
using GaussAuth.Application.Passwords.Ports;
using GaussAuth.Infrastructure.Persistence;
using GaussAuth.Infrastructure.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace GaussAuth.Infrastructure.DependencyInjection;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment? environment = null)
    {
        var connection = configuration.GetConnectionString("AuthenticationDatabase");
        if (string.IsNullOrWhiteSpace(connection))
        {
            throw new InvalidOperationException("Authentication database connection configuration is missing or invalid.");
        }

        try
        {
            var parsed = new NpgsqlConnectionStringBuilder(connection);
            if (!parsed.ContainsKey("Host") || !parsed.ContainsKey("Database") ||
                !parsed.ContainsKey("Username") || string.IsNullOrWhiteSpace(parsed.Host) ||
                string.IsNullOrWhiteSpace(parsed.Database) || string.IsNullOrWhiteSpace(parsed.Username))
            {
                throw new InvalidOperationException("Authentication database connection configuration is missing or invalid.");
            }
        }
        catch (ArgumentException)
        {
            throw new InvalidOperationException("Authentication database connection configuration is missing or invalid.");
        }

        services.AddDbContext<AuthenticationDbContext>(options => options.UseNpgsql(connection));
        services.AddAuthentication();

        var maxFailedAccessAttempts = configuration.GetValue("Identity:Lockout:MaxFailedAccessAttempts", 5);
        var lockoutMinutes = configuration.GetValue("Identity:Lockout:DefaultLockoutMinutes", 5);

        services.AddIdentityCore<IdentityUser<Guid>>(options =>
            {
                options.Stores.SchemaVersion = IdentitySchemaVersions.Version2;
                options.Lockout.MaxFailedAccessAttempts = maxFailedAccessAttempts;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(lockoutMinutes);
                options.Lockout.AllowedForNewUsers = true;
            })
            .AddEntityFrameworkStores<AuthenticationDbContext>()
            .AddSignInManager();

        SessionPolicy sessionPolicy;
        try
        {
            sessionPolicy = new SessionPolicy(
                TimeSpan.FromMinutes(configuration.GetValue("Sessions:SessionLifetimeMinutes", 480)),
                TimeSpan.FromMinutes(configuration.GetValue("Sessions:AccessTokenLifetimeMinutes", 15)));
        }
        catch (ArgumentException)
        {
            throw new InvalidOperationException("Session lifetime configuration is missing or invalid.");
        }
        services.AddSingleton(sessionPolicy);

        var signingKey = AccessCredentialSigningKey.Load(configuration, environment);
        var issuer = configuration["Sessions:Issuer"] is { Length: > 0 } configuredIssuer ? configuredIssuer : "gaussauth";
        services.AddSingleton(signingKey);
        services.AddSingleton<IAccessCredentialKeySet>(signingKey);
        services.AddSingleton<IAccessCredentialIssuer>(new SignedAccessCredentialIssuer(signingKey, issuer));
        services.AddSingleton<IAccessCredentialValidator>(new SignedAccessCredentialValidator(signingKey, issuer));
        services.AddScoped<ISessionRepository, SessionRepository>();

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IApplicationRepository, ApplicationRepository>();
        services.AddScoped<IApplicationMembershipRepository, ApplicationMembershipRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<IPermissionRepository, PermissionRepository>();
        services.AddScoped<IRolePermissionRepository, RolePermissionRepository>();
        services.AddScoped<IUserRoleRepository, UserRoleRepository>();
        services.AddScoped<ICredentialProvisioningService, IdentityCredentialProvisioningService>();
        services.AddScoped<ICredentialVerificationService, IdentityCredentialVerificationService>();
        services.AddScoped<IPasswordCredentialService, IdentityPasswordCredentialService>();
        services.AddScoped<IRecoveryDelivery, ProtectedFileRecoveryDelivery>();
        services.AddScoped<ISecurityEventRecorder, LoggingSecurityEventRecorder>();

        return services;
    }
}
