using Npgsql;

namespace GaussAuth.Foundation.Tests;

/// <summary>
/// A uniquely named, empty PostgreSQL database created on the same server as the test database and dropped on
/// dispose. Used where a test must start from a schema-less database (migration from zero, upgrade, readiness).
/// </summary>
internal sealed class ThrowawayDatabase : IAsyncDisposable
{
    private const string TemplateVariable = "ConnectionStrings__AuthenticationDatabase";
    private const string MaintenanceDatabase = "postgres";

    private readonly string maintenanceConnectionString;

    private ThrowawayDatabase(string name, string connectionString, string maintenanceConnectionString)
    {
        Name = name;
        ConnectionString = connectionString;
        this.maintenanceConnectionString = maintenanceConnectionString;
    }

    /// <summary>The generated database name.</summary>
    public string Name { get; }

    /// <summary>A connection string for the throwaway database.</summary>
    public string ConnectionString { get; }

    public static async Task<ThrowawayDatabase> CreateAsync(CancellationToken cancellationToken = default)
    {
        var template = Environment.GetEnvironmentVariable(TemplateVariable);
        if (string.IsNullOrWhiteSpace(template))
        {
            throw new InvalidOperationException($"{TemplateVariable} must point at the test PostgreSQL server.");
        }

        var name = "gx_throwaway_" + Guid.NewGuid().ToString("N")[..16];
        var maintenance = new NpgsqlConnectionStringBuilder(template) { Database = MaintenanceDatabase, Pooling = false }.ConnectionString;
        var throwaway = new NpgsqlConnectionStringBuilder(template) { Database = name }.ConnectionString;

        await using var connection = new NpgsqlConnection(maintenance);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"CREATE DATABASE \"{name}\"";
        await command.ExecuteNonQueryAsync(cancellationToken);

        return new ThrowawayDatabase(name, throwaway, maintenance);
    }

    public async ValueTask DisposeAsync()
    {
        // Pooled connections to the throwaway database would otherwise keep it alive.
        NpgsqlConnection.ClearAllPools();
        await using var connection = new NpgsqlConnection(maintenanceConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"DROP DATABASE IF EXISTS \"{Name}\" WITH (FORCE)";
        await command.ExecuteNonQueryAsync();
    }
}
