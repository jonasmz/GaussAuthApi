using System.Text.RegularExpressions;
using GaussAuth.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace GaussAuth.Foundation.Tests;

/// <summary>
/// The release SQL script is an operational alternative to the <c>migrate</c> command, not a second evolution mechanism:
/// generated from the same migrations it must yield the same schema and history, be idempotent, upgrade a partially
/// migrated database, and carry no credentials (FR-012b, FR-012d).
/// </summary>
[TestClass]
public sealed class ReleaseMigrationScriptTests
{
    [TestMethod]
    public async Task The_idempotent_script_produces_the_same_schema_and_history_as_the_migrate_command()
    {
        var script = GenerateIdempotentScript();
        await using var viaCommand = await ThrowawayDatabase.CreateAsync();
        await using var viaScript = await ThrowawayDatabase.CreateAsync();

        await MigrationServices.MigrateAsync(viaCommand.ConnectionString);
        await ApplyAsync(viaScript.ConnectionString, script);

        CollectionAssert.AreEqual(ReleaseMigrationValidationTests.ReleaseChain, (await DatabaseSchema.AppliedMigrationsAsync(viaScript.ConnectionString)).ToList());
        CollectionAssert.AreEqual(
            (await DatabaseSchema.DescribeAsync(viaCommand.ConnectionString)).ToList(),
            (await DatabaseSchema.DescribeAsync(viaScript.ConnectionString)).ToList());
        Assert.AreEqual(
            await HistoryAsync(viaCommand.ConnectionString),
            await HistoryAsync(viaScript.ConnectionString),
            "Migration ids and product versions must match.");

        // Seeded data matches too (the data migrations ran identically).
        Assert.AreEqual(
            await DatabaseSchema.CountAsync(viaCommand.ConnectionString, "Permissions"),
            await DatabaseSchema.CountAsync(viaScript.ConnectionString, "Permissions"));
    }

    [TestMethod]
    public async Task The_script_is_idempotent_and_upgrades_a_partially_migrated_database()
    {
        var script = GenerateIdempotentScript();
        await using var database = await ThrowawayDatabase.CreateAsync();

        await MigrateToAsync(database.ConnectionString, "20261001202342_AddSecurityEvents");
        Assert.AreEqual(6, (await DatabaseSchema.AppliedMigrationsAsync(database.ConnectionString)).Count);

        await ApplyAsync(database.ConnectionString, script);
        var afterFirst = await DatabaseSchema.DescribeAsync(database.ConnectionString);
        CollectionAssert.AreEqual(ReleaseMigrationValidationTests.ReleaseChain, (await DatabaseSchema.AppliedMigrationsAsync(database.ConnectionString)).ToList());

        // Running it again (and running the migrate command after it) changes nothing.
        await ApplyAsync(database.ConnectionString, script);
        await MigrationServices.MigrateAsync(database.ConnectionString);
        CollectionAssert.AreEqual(afterFirst.ToList(), (await DatabaseSchema.DescribeAsync(database.ConnectionString)).ToList());
        Assert.AreEqual(10, (await DatabaseSchema.AppliedMigrationsAsync(database.ConnectionString)).Count);
    }

    [TestMethod]
    public void The_script_contains_no_connection_settings_or_credentials()
    {
        var script = GenerateIdempotentScript();

        Assert.IsFalse(Regex.IsMatch(script, @"Host=|Username=|User ID=|Password=|Pwd=", RegexOptions.IgnoreCase), "The script must not embed connection settings.");
        Assert.DoesNotContain("BEGIN PRIVATE KEY", script);
        Assert.Contains("__EFMigrationsHistory", script);
        // Every release migration is represented.
        foreach (var migration in ReleaseMigrationValidationTests.ReleaseChain) Assert.Contains(migration, script);
    }

    private static string GenerateIdempotentScript()
    {
        using var provider = MigrationServices.Build("Host=design-time;Database=design-time;Username=design-time");
        using var scope = provider.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<AuthenticationDbContext>();
        // The same generator `dotnet ef migrations script --idempotent` uses.
        return database.GetService<IMigrator>().GenerateScript(options: MigrationsSqlGenerationOptions.Idempotent);
    }

    private static async Task MigrateToAsync(string connectionString, string target)
    {
        await using var provider = MigrationServices.Build(connectionString);
        using var scope = provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AuthenticationDbContext>().GetService<IMigrator>().MigrateAsync(target);
    }

    private static async Task ApplyAsync(string connectionString, string script)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = script;
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<string> HistoryAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT string_agg(\"MigrationId\" || ':' || \"ProductVersion\", ',' ORDER BY \"MigrationId\") FROM \"__EFMigrationsHistory\"";
        return (string)(await command.ExecuteScalarAsync())!;
    }
}
