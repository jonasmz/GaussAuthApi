using Npgsql;

namespace GaussAuth.Foundation.Tests;

[TestClass]
public sealed class ThrowawayDatabaseTests
{
    [TestMethod]
    public async Task A_throwaway_database_is_created_empty_unique_and_dropped_on_dispose()
    {
        string name;
        string connectionString;
        await using (var first = await ThrowawayDatabase.CreateAsync())
        await using (var second = await ThrowawayDatabase.CreateAsync())
        {
            name = first.Name;
            connectionString = first.ConnectionString;
            Assert.AreNotEqual(first.Name, second.Name);

            await using var connection = new NpgsqlConnection(first.ConnectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT count(*) FROM information_schema.tables WHERE table_schema = 'public'";
            Assert.AreEqual(0L, await command.ExecuteScalarAsync());
        }

        await using var maintenance = new NpgsqlConnection(
            new NpgsqlConnectionStringBuilder(connectionString) { Database = "postgres", Pooling = false }.ConnectionString);
        await maintenance.OpenAsync();
        await using var exists = maintenance.CreateCommand();
        exists.CommandText = "SELECT count(*) FROM pg_database WHERE datname = @name";
        exists.Parameters.AddWithValue("name", name);
        Assert.AreEqual(0L, await exists.ExecuteScalarAsync());
    }
}
