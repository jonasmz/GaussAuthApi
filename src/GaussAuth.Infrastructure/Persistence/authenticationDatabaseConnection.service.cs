using GaussAuth.Infrastructure.Configuration;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace GaussAuth.Infrastructure.Persistence;

/// <summary>Reads and validates the authentication database connection string without ever echoing it.</summary>
public static class AuthenticationDatabaseConnection
{
    public const string SettingKey = "ConnectionStrings:AuthenticationDatabase";

    public static string Read(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var connection = configuration.GetConnectionString("AuthenticationDatabase");
        if (string.IsNullOrWhiteSpace(connection))
        {
            throw new StartupConfigurationException(SettingKey, "is required: configure a PostgreSQL connection string");
        }

        NpgsqlConnectionStringBuilder parsed;
        try
        {
            parsed = new NpgsqlConnectionStringBuilder(connection);
        }
        catch (ArgumentException)
        {
            throw new StartupConfigurationException(SettingKey, "is not a valid PostgreSQL connection string");
        }

        if (!parsed.ContainsKey("Host") || !parsed.ContainsKey("Database") || !parsed.ContainsKey("Username") ||
            string.IsNullOrWhiteSpace(parsed.Host) || string.IsNullOrWhiteSpace(parsed.Database) || string.IsNullOrWhiteSpace(parsed.Username))
        {
            throw new StartupConfigurationException(SettingKey, "must specify Host, Database and Username");
        }

        return connection;
    }
}
