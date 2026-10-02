using GaussAuth.Application.Administration.Ports;
using GaussAuth.Infrastructure.Administration;
using GaussAuth.Infrastructure.Configuration;
using GaussAuth.Application.Users.Ports;
using GaussAuth.Application.Applications.Ports;
using GaussAuth.Application.Memberships.Ports;
using GaussAuth.Application.Roles.Ports;
using GaussAuth.Application.Permissions.Ports;
using GaussAuth.Application.Authorization.Ports;
using GaussAuth.Application.AuthorizationContext.Ports;
using GaussAuth.Application.Login.Ports;
using GaussAuth.Application.Security.Ports;
using GaussAuth.Application.Security;
using GaussAuth.Application.Sessions;
using GaussAuth.Application.Sessions.Ports;
using GaussAuth.Infrastructure.Sessions;
using Microsoft.Extensions.Hosting;
using GaussAuth.Infrastructure.Identity;
using GaussAuth.Infrastructure.AuthorizationContext;
using GaussAuth.Infrastructure.Passwords;
using GaussAuth.Application.Passwords.Ports;
using GaussAuth.Application.Profiles.Avatars;
using GaussAuth.Application.Profiles.Avatars.Ports;
using GaussAuth.Infrastructure.ProfileImages;
using GaussAuth.Infrastructure.Persistence;
using GaussAuth.Infrastructure.Security;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
        const string connectionKey = "ConnectionStrings:AuthenticationDatabase";
        var connection = configuration.GetConnectionString("AuthenticationDatabase");
        if (string.IsNullOrWhiteSpace(connection))
        {
            throw new StartupConfigurationException(connectionKey, "is required: configure a PostgreSQL connection string");
        }

        NpgsqlConnectionStringBuilder parsed;
        try
        {
            parsed = new NpgsqlConnectionStringBuilder(connection);
        }
        catch (ArgumentException)
        {
            throw new StartupConfigurationException(connectionKey, "is not a valid PostgreSQL connection string");
        }

        if (!parsed.ContainsKey("Host") || !parsed.ContainsKey("Database") || !parsed.ContainsKey("Username") ||
            string.IsNullOrWhiteSpace(parsed.Host) || string.IsNullOrWhiteSpace(parsed.Database) || string.IsNullOrWhiteSpace(parsed.Username))
        {
            throw new StartupConfigurationException(connectionKey, "must specify Host, Database and Username");
        }

        services.AddDbContext<AuthenticationDbContext>(options => options.UseNpgsql(connection));
        services.AddAuthentication();

        var maxFailedAccessAttempts = configuration.GetBoundedInt32("Identity:Lockout:MaxFailedAccessAttempts", 5, 1, 1_000);
        var lockoutMinutes = configuration.GetBoundedInt32("Identity:Lockout:DefaultLockoutMinutes", 5, 1, 525_600);

        services.AddIdentityCore<IdentityUser<Guid>>(options =>
            {
                options.Stores.SchemaVersion = IdentitySchemaVersions.Version2;
                options.Lockout.MaxFailedAccessAttempts = maxFailedAccessAttempts;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(lockoutMinutes);
                options.Lockout.AllowedForNewUsers = true;
            })
            .AddEntityFrameworkStores<AuthenticationDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders();

        services.AddSingleton(new GaussAuth.Application.Administration.AdministrationOptions(
            configuration.GetBoundedInt32(
                "Administration:MaxBulkSessionRevocation",
                GaussAuth.Application.Administration.AdministrationOptions.DefaultMaxBulkSessionRevocation,
                1,
                GaussAuth.Application.Administration.AdministrationOptions.MaximumAllowedBulkSessionRevocation)));

        var sessionMinutes = configuration.GetBoundedInt32("Sessions:SessionLifetimeMinutes", 480, 1, 525_600);
        var accessMinutes = configuration.GetBoundedInt32("Sessions:AccessTokenLifetimeMinutes", 15, 1, 525_600);
        if (accessMinutes > sessionMinutes)
        {
            throw new StartupConfigurationException("Sessions:AccessTokenLifetimeMinutes", "must not exceed Sessions:SessionLifetimeMinutes");
        }

        var sessionPolicy = new SessionPolicy(TimeSpan.FromMinutes(sessionMinutes), TimeSpan.FromMinutes(accessMinutes));
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
        services.AddSingleton(new ConfiguredConsumerCredentialValidator(configuration));
        services.AddScoped<IConsumerCredentialValidator, StoreBackedConsumerCredentialValidator>();
        services.AddScoped<IConsumerCredentialStore, EfConsumerCredentialStore>();
        services.AddScoped<ICredentialProvisioningService, IdentityCredentialProvisioningService>();
        services.AddScoped<ICredentialVerificationService, IdentityCredentialVerificationService>();
        services.AddScoped<IPasswordCredentialService, IdentityPasswordCredentialService>();
        services.AddScoped<IRecoveryDelivery, ProtectedFileRecoveryDelivery>();
        services.AddSingleton(SecurityAuditRetentionPolicy.Load(configuration));
        services.AddSingleton<SecurityEventCatalog>();
        services.AddScoped<ISecurityEventRepository, SecurityEventRepository>();
        services.AddScoped<ISecurityEventQueryRepository, SecurityEventQueryRepository>();
        services.AddSingleton<IGlobalAdministratorPolicy>(new ConfiguredGlobalAdministratorPolicy(configuration));
        services.AddScoped<ISecurityEventRecorder, PersistedSecurityEventRecorder>();

        var keyRing = KeyRingOptions.Load(configuration, environment);
        services.AddSingleton(keyRing);
        var dataProtection = services.AddDataProtection().SetApplicationName("GaussAuth");
        if (keyRing.KeysPath is not null) dataProtection.PersistKeysToFileSystem(new DirectoryInfo(keyRing.KeysPath));

        var profileImages = ProfileImagesOptions.Load(configuration, environment);
        services.AddSingleton(profileImages);
        services.AddSingleton(profileImages.Limits);
        services.AddSingleton<IProfileImageProcessor>(new SkiaProfileImageProcessor(profileImages.Limits));
        services.AddSingleton<IProfileImageStorage>(provider => new LocalProfileImageStorage(
            profileImages, provider.GetRequiredService<ILogger<LocalProfileImageStorage>>()));

        services.AddHostedService<StartupDiagnostics>();

        return services;
    }
}
