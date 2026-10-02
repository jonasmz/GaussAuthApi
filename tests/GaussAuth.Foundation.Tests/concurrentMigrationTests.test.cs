using GaussAuth.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace GaussAuth.Foundation.Tests;

/// <summary>
/// Two operators (or two pipeline runs) starting <c>migrate</c> at the same moment must not corrupt the schema. The
/// migration contract states what is verified here rather than assumed.
/// </summary>
[TestClass]
public sealed class ConcurrentMigrationTests
{
    [TestMethod]
    public async Task Two_simultaneous_migrate_commands_leave_one_complete_consistent_schema()
    {
        for (var round = 1; round <= 3; round++)
        {
            await using var concurrent = await ThrowawayDatabase.CreateAsync();
            await using var reference = await ThrowawayDatabase.CreateAsync();
            await MigrationServices.MigrateAsync(reference.ConnectionString);

            var environment = new Dictionary<string, string?> { ["ConnectionStrings__AuthenticationDatabase"] = concurrent.ConnectionString };
            var results = await Task.WhenAll(
                ApiProcess.RunAsync(environment, TimeSpan.FromSeconds(90), "migrate"),
                ApiProcess.RunAsync(environment, TimeSpan.FromSeconds(90), "migrate"));

            foreach (var result in results) Assert.IsFalse(result.TimedOut, $"round {round}: {result.Output}");
            Assert.IsTrue(results.Any(result => result.ExitCode == 0), $"round {round}: at least one migrator must succeed. {string.Join(" | ", results.Select(r => r.Output))}");
            // Whatever each process reported, the database must end up complete and structurally identical to a normal run.
            CollectionAssert.AreEqual(ReleaseMigrationValidationTests.ReleaseChain, (await DatabaseSchema.AppliedMigrationsAsync(concurrent.ConnectionString)).ToList(), $"round {round}");
            CollectionAssert.AreEqual(
                (await DatabaseSchema.DescribeAsync(reference.ConnectionString)).ToList(),
                (await DatabaseSchema.DescribeAsync(concurrent.ConnectionString)).ToList(),
                $"round {round}");
            Console.WriteLine($"round {round}: exit codes {string.Join(",", results.Select(r => r.ExitCode))}");
        }
    }

    [TestMethod]
    public async Task A_held_migration_lock_makes_a_second_run_report_locked_and_change_nothing_until_released()
    {
        await using var database = await ThrowawayDatabase.CreateAsync();
        await using var holder = new NpgsqlConnection(database.ConnectionString);
        await holder.OpenAsync();
        await using (var acquire = holder.CreateCommand())
        {
            acquire.CommandText = $"SELECT pg_advisory_lock({DatabaseMigrator.AdvisoryLockKey})";
            await acquire.ExecuteScalarAsync();
        }

        await using var provider = MigrationServices.Build(database.ConnectionString);
        using (var scope = provider.CreateScope())
        {
            var migrator = new DatabaseMigrator(scope.ServiceProvider.GetRequiredService<AuthenticationDbContext>(), NullLogger<DatabaseMigrator>.Instance)
            {
                LockTimeout = TimeSpan.FromSeconds(1)
            };

            var blocked = await migrator.MigrateAsync(CancellationToken.None);

            Assert.IsFalse(blocked.Succeeded);
            Assert.AreEqual(DatabaseMigrationResult.LockedCategory, blocked.FailureCategory);
            Assert.AreEqual(0, blocked.AppliedMigrations.Count);
        }

        await using (var tables = new NpgsqlConnection(database.ConnectionString))
        {
            await tables.OpenAsync();
            await using var count = tables.CreateCommand();
            count.CommandText = "SELECT count(*) FROM information_schema.tables WHERE table_schema = 'public'";
            Assert.AreEqual(0L, Convert.ToInt64(await count.ExecuteScalarAsync()), "A run that never got the lock must not touch the schema.");
        }

        await using (var release = holder.CreateCommand())
        {
            release.CommandText = $"SELECT pg_advisory_unlock({DatabaseMigrator.AdvisoryLockKey})";
            await release.ExecuteScalarAsync();
        }

        using var retryScope = provider.CreateScope();
        var retried = await retryScope.ServiceProvider.GetRequiredService<DatabaseMigrator>().MigrateAsync(CancellationToken.None);
        Assert.IsTrue(retried.Succeeded);
        Assert.AreEqual(10, retried.AppliedMigrations.Count);
    }
}
