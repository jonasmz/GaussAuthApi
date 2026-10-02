using GaussAuth.Infrastructure.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.DependencyInjection;

namespace GaussAuth.Infrastructure.Persistence;

/// <summary>
/// Used only by the EF Core tooling (<c>dotnet ef migrations script</c>) so SQL can be generated from the migrations
/// assembly without booting the API or supplying any of its settings or secrets. Generating a script never connects to
/// the database, so a placeholder connection string is used when none is supplied.
/// </summary>
public sealed class AuthenticationDbContextDesignTimeFactory : IDesignTimeDbContextFactory<AuthenticationDbContext>
{
    private const string PlaceholderConnection = "Host=design-time;Database=design-time;Username=design-time";

    public AuthenticationDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__AuthenticationDatabase");
        var services = new ServiceCollection();
        // The same registration as the API and the migrate command, so the design-time model is identical.
        services.AddAuthenticationPersistence(string.IsNullOrWhiteSpace(connection) ? PlaceholderConnection : connection);
        var provider = services.BuildServiceProvider();
        return provider.CreateScope().ServiceProvider.GetRequiredService<AuthenticationDbContext>();
    }
}
