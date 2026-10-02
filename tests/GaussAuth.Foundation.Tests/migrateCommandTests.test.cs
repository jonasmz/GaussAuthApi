using Npgsql;

namespace GaussAuth.Foundation.Tests;

/// <summary>
/// The one-off <c>migrate</c> command run as a real process: success and no-op, value-free actionable failures, and the
/// guarantee that the web host never migrates on its own (FR-011, FR-012, FR-012a).
/// </summary>
[TestClass]
public sealed class MigrateCommandTests
{
    private const string Sentinel = "SENTINEL_9f3a";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);

    [TestMethod]
    public async Task Migrate_applies_every_migration_to_an_empty_database_and_is_a_no_op_afterwards()
    {
        await using var database = await ThrowawayDatabase.CreateAsync();

        var first = await RunMigrateAsync(database.ConnectionString);
        Assert.IsFalse(first.TimedOut, first.Output);
        Assert.AreEqual(0, first.ExitCode, first.Output);
        Assert.Contains("applied 10 migration(s)", first.Output);
        Assert.Contains("20261001012324_InitialIdentityFoundation", first.Output);
        Assert.Contains("20261002010000_MoveAuditPermissionToAuthNamespace", first.Output);
        CollectionAssert.AreEqual(ReleaseMigrationValidationTests.ReleaseChain, (await DatabaseSchema.AppliedMigrationsAsync(database.ConnectionString)).ToList());

        var second = await RunMigrateAsync(database.ConnectionString);
        Assert.AreEqual(0, second.ExitCode, second.Output);
        Assert.Contains("already up to date", second.Output);
    }

    [TestMethod]
    public async Task Migrate_against_an_unreachable_server_fails_with_a_connection_category_and_no_secrets()
    {
        var connection = $"Host=127.0.0.1;Port=1;Database=nowhere;Username=nobody;Password={Sentinel};Timeout=3;Command Timeout=3";

        var result = await RunMigrateAsync(connection);

        AssertFailed(result, "Category connection");
        AssertNoLeak(result.Output, Sentinel, "nobody", "127.0.0.1", "nowhere");
    }

    [TestMethod]
    public async Task Migrate_with_rejected_credentials_fails_with_an_authentication_category_and_no_secrets()
    {
        var template = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("ConnectionStrings__AuthenticationDatabase"))
        {
            Username = "gx_unknown_role",
            Password = Sentinel,
            Pooling = false
        };

        var result = await RunMigrateAsync(template.ConnectionString);

        AssertFailed(result, "Category authentication");
        AssertNoLeak(result.Output, Sentinel, "gx_unknown_role", template.Database!);
    }

    [DataRow("")]
    [DataRow("not a connection string " + Sentinel)]
    [DataRow("Host=localhost")]
    [TestMethod]
    public async Task Migrate_with_a_missing_or_invalid_connection_string_names_only_the_setting(string connection)
    {
        var result = await RunMigrateAsync(connection);

        Assert.IsFalse(result.TimedOut, result.Output);
        Assert.AreNotEqual(0, result.ExitCode);
        Assert.Contains("ConnectionStrings:AuthenticationDatabase", result.Output);
        AssertNoLeak(result.Output, Sentinel, "Host=localhost");
    }

    [TestMethod]
    public async Task A_failing_migration_names_the_migration_and_leaves_no_sql_or_stack_trace_in_the_output()
    {
        await using var database = await ThrowawayDatabase.CreateAsync();
        await using (var connection = new NpgsqlConnection(database.ConnectionString))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            // A conflicting object makes the very first migration fail partway.
            command.CommandText = "CREATE TABLE \"AspNetUsers\" (x integer)";
            await command.ExecuteNonQueryAsync();
        }

        var result = await RunMigrateAsync(database.ConnectionString);

        AssertFailed(result, "Category migration-failed");
        Assert.Contains("20261001012324_InitialIdentityFoundation", result.Output);
        Assert.Contains("42P07", result.Output);
        AssertNoLeak(result.Output, "CREATE TABLE", "AspNetUserClaims");
    }

    [TestMethod]
    public async Task The_web_host_never_applies_migrations_on_startup()
    {
        await using var database = await ThrowawayDatabase.CreateAsync();
        var environment = ProductionApiEnvironment.Create("Production", database.ConnectionString);

        var result = await ApiProcess.RunAsync(environment, TimeSpan.FromSeconds(60), output => output.Contains("Application started"));

        Assert.AreEqual(-2, result.ExitCode, result.Output);
        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT count(*) FROM information_schema.tables WHERE table_schema = 'public'";
        Assert.AreEqual(0L, Convert.ToInt64(await command.ExecuteScalarAsync()), "The running service must not create or change schema.");
    }

    private static Task<(int ExitCode, string Output, bool TimedOut)> RunMigrateAsync(string connection) =>
        ApiProcess.RunAsync(
            new Dictionary<string, string?> { ["ConnectionStrings__AuthenticationDatabase"] = connection },
            Timeout,
            "migrate");

    private static void AssertFailed((int ExitCode, string Output, bool TimedOut) result, string expectedCategory)
    {
        Assert.IsFalse(result.TimedOut, result.Output);
        Assert.AreEqual(1, result.ExitCode, result.Output);
        Assert.Contains(expectedCategory, result.Output);
        Assert.Contains("Migration failed", result.Output);
        Assert.DoesNotContain("   at ", result.Output);
        Assert.DoesNotContain("Unhandled exception", result.Output);
    }

    private static void AssertNoLeak(string output, params string[] forbidden)
    {
        foreach (var value in forbidden) Assert.DoesNotContain(value, output, $"The output must not contain '{value}'.");
        Assert.DoesNotContain("Password", output);
    }
}
