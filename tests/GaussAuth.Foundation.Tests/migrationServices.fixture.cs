using GaussAuth.Infrastructure.DependencyInjection;
using GaussAuth.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace GaussAuth.Foundation.Tests;

/// <summary>Builds the same persistence registration the API and the <c>migrate</c> command use, for a given database.</summary>
internal static class MigrationServices
{
    public static ServiceProvider Build(string connectionString)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthenticationPersistence(connectionString);
        services.AddScoped<DatabaseMigrator>();
        return services.BuildServiceProvider();
    }

    public static async Task MigrateAsync(string connectionString)
    {
        await using var provider = Build(connectionString);
        using var scope = provider.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<DatabaseMigrator>().MigrateAsync(CancellationToken.None);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException($"Migration failed: {result.FailureCategory} {result.FailedMigration} {result.FailureCode}");
        }
    }
}
