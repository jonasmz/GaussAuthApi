using System.Net.Sockets;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace GaussAuth.Infrastructure.Persistence;

/// <summary>
/// Applies pending EF Core migrations one at a time so each applied migration is logged and a failure identifies the
/// exact migration. Used only by the explicit <c>migrate</c> command; the running API never migrates.
/// <para>
/// The whole run holds a PostgreSQL session advisory lock, so two migrators started at the same moment are serialized:
/// the second waits, then finds nothing pending. (Relying on the framework's own per-call locking was verified not to be
/// enough: concurrent runs failed with PostgreSQL errors 42704 and 2BP01.)
/// </para>
/// </summary>
public sealed class DatabaseMigrator(AuthenticationDbContext database, ILogger<DatabaseMigrator> logger)
{
    /// <summary>Advisory lock key held for the duration of a migration run (the bytes of "GaussMig").</summary>
    public const long AdvisoryLockKey = 0x47617573734D6967;

    /// <summary>How long a run waits for another migrator to finish before giving up.</summary>
    public TimeSpan LockTimeout { get; init; } = TimeSpan.FromMinutes(5);

    public async Task<DatabaseMigrationResult> MigrateAsync(CancellationToken cancellationToken)
    {
        var applied = new List<string>();
        string? current = null;
        var lockHeld = false;
        try
        {
            await database.Database.OpenConnectionAsync(cancellationToken);
            if (!await AcquireLockAsync(cancellationToken))
            {
                return DatabaseMigrationResult.Failure(applied, DatabaseMigrationResult.LockedCategory, null, "lock-timeout");
            }

            lockHeld = true;
            var pending = (await database.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
            if (pending.Count == 0)
            {
                logger.LogInformation("The database schema is already up to date; no migrations were applied.");
                return DatabaseMigrationResult.Success(applied);
            }

            logger.LogInformation("Applying {Count} pending migration(s).", pending.Count);
            var migrator = database.GetService<IMigrator>();
            foreach (var migration in pending)
            {
                current = migration;
                await migrator.MigrateAsync(migration, cancellationToken);
                applied.Add(migration);
                logger.LogInformation("Applied migration {Migration}.", migration);
            }

            logger.LogInformation("The database schema is up to date; applied {Count} migration(s).", applied.Count);
            return DatabaseMigrationResult.Success(applied);
        }
        catch (OperationCanceledException)
        {
            return DatabaseMigrationResult.Failure(applied, DatabaseMigrationResult.CancelledCategory, current, nameof(OperationCanceledException));
        }
        catch (Exception exception)
        {
            return Classify(exception, applied, current);
        }
        finally
        {
            await ReleaseAsync(lockHeld);
        }
    }

    // pg_try_advisory_lock polled rather than the blocking variant so a stuck migrator cannot hang the deployment forever.
    private async Task<bool> AcquireLockAsync(CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + LockTimeout;
        var announced = false;
        while (true)
        {
            await using var command = database.Database.GetDbConnection().CreateCommand();
            command.CommandText = "SELECT pg_try_advisory_lock(@key)";
            var key = command.CreateParameter();
            key.ParameterName = "key";
            key.Value = AdvisoryLockKey;
            command.Parameters.Add(key);
            if ((bool)(await command.ExecuteScalarAsync(cancellationToken))!) return true;

            if (!announced)
            {
                logger.LogInformation("Another migration run holds the lock; waiting for it to finish.");
                announced = true;
            }

            if (DateTime.UtcNow >= deadline) return false;
            await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken);
        }
    }

    private async Task ReleaseAsync(bool lockHeld)
    {
        try
        {
            if (lockHeld)
            {
                await using var command = database.Database.GetDbConnection().CreateCommand();
                command.CommandText = "SELECT pg_advisory_unlock(@key)";
                var key = command.CreateParameter();
                key.ParameterName = "key";
                key.Value = AdvisoryLockKey;
                command.Parameters.Add(key);
                await command.ExecuteScalarAsync();
            }
        }
        catch (Exception)
        {
            // The lock is session-scoped: it is released anyway when the connection closes below.
        }
        finally
        {
            try
            {
                await database.Database.CloseConnectionAsync();
            }
            catch (Exception)
            {
                // Nothing useful to do while reporting the outcome of the run.
            }
        }
    }

    private static DatabaseMigrationResult Classify(Exception exception, List<string> applied, string? current)
    {
        var root = Unwrap(exception);
        return root switch
        {
            PostgresException { SqlState: "28P01" or "28000" } postgres =>
                DatabaseMigrationResult.Failure(applied, DatabaseMigrationResult.AuthenticationCategory, null, postgres.SqlState),
            PostgresException postgres when postgres.SqlState is "3D000" or "57P03" or "53300" || postgres.SqlState.StartsWith("08", StringComparison.Ordinal) =>
                DatabaseMigrationResult.Failure(applied, DatabaseMigrationResult.ConnectionCategory, null, postgres.SqlState),
            PostgresException postgres =>
                DatabaseMigrationResult.Failure(applied, DatabaseMigrationResult.MigrationFailedCategory, current, postgres.SqlState),
            NpgsqlException or SocketException or TimeoutException or IOException =>
                DatabaseMigrationResult.Failure(applied, DatabaseMigrationResult.ConnectionCategory, null, root.GetType().Name),
            _ =>
                DatabaseMigrationResult.Failure(applied, DatabaseMigrationResult.MigrationFailedCategory, current, root.GetType().Name)
        };
    }

    // EF and the provider wrap the driver exception; classification must look at the innermost meaningful one.
    private static Exception Unwrap(Exception exception)
    {
        for (var candidate = exception; candidate is not null; candidate = candidate.InnerException)
        {
            if (candidate is PostgresException or NpgsqlException or SocketException) return candidate;
        }

        return exception;
    }
}
