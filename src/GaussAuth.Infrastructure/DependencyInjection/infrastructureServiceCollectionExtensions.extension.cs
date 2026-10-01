using GaussAuth.Application.Users.Ports;
using GaussAuth.Infrastructure.Identity;
using GaussAuth.Infrastructure.Persistence;
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
        IConfiguration configuration)
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
        services.AddIdentityCore<IdentityUser<Guid>>(options =>
                options.Stores.SchemaVersion = IdentitySchemaVersions.Version2)
            .AddEntityFrameworkStores<AuthenticationDbContext>();

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ICredentialProvisioningService, IdentityCredentialProvisioningService>();

        return services;
    }
}
