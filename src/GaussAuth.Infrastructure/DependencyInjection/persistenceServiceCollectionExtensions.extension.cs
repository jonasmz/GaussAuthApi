using GaussAuth.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GaussAuth.Infrastructure.DependencyInjection;

public static class PersistenceServiceCollectionExtensions
{
    /// <summary>
    /// Registers the authentication database context together with the Identity store configuration that shapes its
    /// model. The API host and the one-off migration host both use this method so they always build the identical model
    /// (a different Identity schema version would make the model drift from the migrations).
    /// </summary>
    public static IdentityBuilder AddAuthenticationPersistence(
        this IServiceCollection services,
        string connectionString,
        Action<IdentityOptions>? configureIdentity = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.AddDbContext<AuthenticationDbContext>(options => options.UseNpgsql(connectionString));
        return services.AddIdentityCore<IdentityUser<Guid>>(options =>
            {
                options.Stores.SchemaVersion = IdentitySchemaVersions.Version2;
                configureIdentity?.Invoke(options);
            })
            .AddEntityFrameworkStores<AuthenticationDbContext>();
    }
}
