using GaussAuth.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace GaussAuth.Foundation.Tests;

/// <summary>
/// Upgrade from the previous release schema state (everything through <c>AddSecurityEvents</c>, i.e. features 001–010) to
/// the final schema with representative data: nothing is lost, the 011 data migrations behave as documented, and the
/// upgraded structure equals a database migrated from zero (FR-009, FR-010, SC-003).
/// </summary>
[TestClass]
public sealed class ReleaseMigrationUpgradeTests
{
    private const string PreviousReleaseState = "20261001202342_AddSecurityEvents";

    private static readonly Guid User1 = Guid.Parse("11111111-0000-0000-0000-000000000001");
    private static readonly Guid User2 = Guid.Parse("11111111-0000-0000-0000-000000000002");
    private static readonly Guid App1 = Guid.Parse("22222222-0000-0000-0000-000000000001");
    private static readonly Guid App2 = Guid.Parse("22222222-0000-0000-0000-000000000002");
    private static readonly Guid Role1 = Guid.Parse("33333333-0000-0000-0000-000000000001");
    private static readonly Guid Role2 = Guid.Parse("33333333-0000-0000-0000-000000000002");
    private static readonly Guid LegacyAudit1 = Guid.Parse("44444444-0000-0000-0000-000000000001");
    private static readonly Guid Business1 = Guid.Parse("44444444-0000-0000-0000-000000000002");
    private static readonly Guid LegacyAudit2 = Guid.Parse("44444444-0000-0000-0000-000000000003");

    private static readonly string[] DataTables =
        ["AspNetUsers", "Users", "UserProfiles", "Applications", "ApplicationMemberships", "Roles", "UserRoles", "Sessions", "SecurityEvents"];

    [TestMethod]
    public async Task Upgrading_from_the_previous_release_with_data_preserves_everything_and_matches_a_fresh_schema()
    {
        await using var upgraded = await ThrowawayDatabase.CreateAsync();
        await using var fresh = await ThrowawayDatabase.CreateAsync();

        await MigrateToAsync(upgraded.ConnectionString, PreviousReleaseState);
        CollectionAssert.AreEqual(
            ReleaseMigrationValidationTests.ReleaseChain.Take(6).ToList(),
            (await DatabaseSchema.AppliedMigrationsAsync(upgraded.ConnectionString)).ToList());
        await SeedPreviousReleaseDataAsync(upgraded.ConnectionString);

        var before = new Dictionary<string, long>();
        foreach (var table in DataTables.Append("Permissions").Append("RolePermissions"))
            before[table] = await DatabaseSchema.CountAsync(upgraded.ConnectionString, table);

        await MigrationServices.MigrateAsync(upgraded.ConnectionString);
        await MigrationServices.MigrateAsync(fresh.ConnectionString);

        CollectionAssert.AreEqual(ReleaseMigrationValidationTests.ReleaseChain, (await DatabaseSchema.AppliedMigrationsAsync(upgraded.ConnectionString)).ToList());

        // No user, security, authorization-history or audit row is lost or rewritten.
        foreach (var table in DataTables)
            Assert.AreEqual(before[table], await DatabaseSchema.CountAsync(upgraded.ConnectionString, table), $"{table} lost or gained rows.");
        Assert.AreEqual(1L, await DatabaseSchema.CountAsync(upgraded.ConnectionString, "Users", $"\"Id\" = '{User1}' AND \"Email\" = 'one@example.test' AND \"IsActive\""));
        Assert.AreEqual(1L, await DatabaseSchema.CountAsync(upgraded.ConnectionString, "Sessions", "\"RevokedAt\" IS NOT NULL"));
        Assert.AreEqual(2L, await DatabaseSchema.CountAsync(upgraded.ConnectionString, "SecurityEvents", "\"ActorUserId\" IS NULL"));
        Assert.AreEqual(1L, await DatabaseSchema.CountAsync(upgraded.ConnectionString, "SecurityEvents", "\"EventType\" = 'LoginFailed' AND \"Outcome\" = 'Failure'"));

        // Existing authorization rows are kept: the three original role assignments and every original permission id.
        Assert.AreEqual(3L, await DatabaseSchema.CountAsync(upgraded.ConnectionString, "RolePermissions",
            $"\"Id\" IN ('{Guid.Parse("55555555-0000-0000-0000-000000000001")}','{Guid.Parse("55555555-0000-0000-0000-000000000002")}','{Guid.Parse("55555555-0000-0000-0000-000000000003")}')"));
        Assert.AreEqual(3L, await DatabaseSchema.CountAsync(upgraded.ConnectionString, "Permissions", $"\"Id\" IN ('{LegacyAudit1}','{Business1}','{LegacyAudit2}')"));

        // 011 data migrations: administrative permissions seeded exactly once per Application (9 each, 2 Applications).
        Assert.AreEqual(before["Permissions"] + 18, await DatabaseSchema.CountAsync(upgraded.ConnectionString, "Permissions"));
        foreach (var app in new[] { App1, App2 })
        {
            Assert.AreEqual(9L, await DatabaseSchema.CountAsync(upgraded.ConnectionString, "Permissions", $"\"ApplicationId\" = '{app}' AND \"Code\" LIKE 'auth.%' AND \"IsActive\""));
            Assert.AreEqual(1L, await DatabaseSchema.CountAsync(upgraded.ConnectionString, "Permissions", $"\"ApplicationId\" = '{app}' AND \"Code\" = 'auth.security.audit.read'"));
        }

        // The audit permission is renamed with assignments intact: the legacy permission is deactivated (kept as history)
        // and every reviewer role keeps access through a copied, active assignment to the platform permission — per Application.
        Assert.AreEqual(2L, await DatabaseSchema.CountAsync(upgraded.ConnectionString, "Permissions", "\"Code\" = 'audit.events.read' AND NOT \"IsActive\""));
        Assert.AreEqual(1L, await DatabaseSchema.CountAsync(upgraded.ConnectionString, "RolePermissions",
            $"\"RoleId\" = '{Role1}' AND \"ApplicationId\" = '{App1}' AND \"IsActive\" AND \"PermissionId\" IN (SELECT \"Id\" FROM \"Permissions\" WHERE \"ApplicationId\" = '{App1}' AND \"Code\" = 'auth.security.audit.read')"));
        Assert.AreEqual(1L, await DatabaseSchema.CountAsync(upgraded.ConnectionString, "RolePermissions",
            $"\"RoleId\" = '{Role2}' AND \"ApplicationId\" = '{App2}' AND \"IsActive\" AND \"PermissionId\" IN (SELECT \"Id\" FROM \"Permissions\" WHERE \"ApplicationId\" = '{App2}' AND \"Code\" = 'auth.security.audit.read')"));
        // Isolation: a role never gains the platform permission of another Application, and business assignments are untouched.
        Assert.AreEqual(0L, await DatabaseSchema.CountAsync(upgraded.ConnectionString, "RolePermissions",
            $"\"RoleId\" = '{Role2}' AND \"PermissionId\" IN (SELECT \"Id\" FROM \"Permissions\" WHERE \"ApplicationId\" = '{App1}')"));
        Assert.AreEqual(1L, await DatabaseSchema.CountAsync(upgraded.ConnectionString, "RolePermissions", $"\"RoleId\" = '{Role1}' AND \"PermissionId\" = '{Business1}' AND \"IsActive\""));

        // The upgraded structure is identical to a database created from zero.
        CollectionAssert.AreEqual(
            (await DatabaseSchema.DescribeAsync(fresh.ConnectionString)).ToList(),
            (await DatabaseSchema.DescribeAsync(upgraded.ConnectionString)).ToList());
    }

    [TestMethod]
    public async Task A_reserved_permission_prefix_makes_the_seed_migration_fail_closed_without_changing_data()
    {
        await using var database = await ThrowawayDatabase.CreateAsync();
        await MigrateToAsync(database.ConnectionString, PreviousReleaseState);
        await SeedPreviousReleaseDataAsync(database.ConnectionString);
        await ExecuteAsync(database.ConnectionString,
            $"INSERT INTO \"Permissions\" (\"Id\", \"ApplicationId\", \"Code\", \"IsActive\", \"CreatedAt\", \"UpdatedAt\") VALUES ('{Guid.NewGuid()}', '{App1}', 'auth.custom', TRUE, now(), now())");
        var permissionsBefore = await DatabaseSchema.CountAsync(database.ConnectionString, "Permissions");

        await using var provider = MigrationServices.Build(database.ConnectionString);
        using var scope = provider.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<DatabaseMigrator>().MigrateAsync(CancellationToken.None);

        Assert.IsFalse(result.Succeeded);
        Assert.AreEqual(DatabaseMigrationResult.MigrationFailedCategory, result.FailureCategory);
        Assert.AreEqual("20261002000000_SeedAdministrativePermissions", result.FailedMigration);
        Assert.AreEqual("P0001", result.FailureCode);
        // The failed migration rolled back: history stops at the last applied one and no permission was touched.
        CollectionAssert.AreEqual(
            ReleaseMigrationValidationTests.ReleaseChain.Take(7).ToList(),
            (await DatabaseSchema.AppliedMigrationsAsync(database.ConnectionString)).ToList());
        Assert.AreEqual(permissionsBefore, await DatabaseSchema.CountAsync(database.ConnectionString, "Permissions"));
    }

    private static async Task MigrateToAsync(string connectionString, string targetMigration)
    {
        await using var provider = MigrationServices.Build(connectionString);
        using var scope = provider.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<AuthenticationDbContext>();
        await database.GetService<IMigrator>().MigrateAsync(targetMigration);
    }

    private static async Task SeedPreviousReleaseDataAsync(string connectionString)
    {
        foreach (var (user, email) in new[] { (User1, "one@example.test"), (User2, "two@example.test") })
        {
            await ExecuteAsync(connectionString, $"""
                INSERT INTO "AspNetUsers" ("Id","UserName","NormalizedUserName","Email","NormalizedEmail","EmailConfirmed","PasswordHash","SecurityStamp","ConcurrencyStamp","PhoneNumberConfirmed","TwoFactorEnabled","LockoutEnabled","AccessFailedCount")
                VALUES ('{user}','{email}','{email.ToUpperInvariant()}','{email}','{email.ToUpperInvariant()}',FALSE,'not-a-real-hash','stamp','concurrency',FALSE,FALSE,TRUE,0);
                INSERT INTO "Users" ("Id","Email","NormalizedEmail","IsActive","CreatedAt","UpdatedAt")
                VALUES ('{user}','{email}','{email.ToUpperInvariant()}',TRUE,now(),now());
                INSERT INTO "UserProfiles" ("UserId","FirstName","LastName","DisplayName","CreatedAt","UpdatedAt")
                VALUES ('{user}','First','Last','Display',now(),now());
                """);
        }

        await ExecuteAsync(connectionString, $"""
            INSERT INTO "Applications" ("Id","Code","Name","IsActive","CreatedAt","UpdatedAt") VALUES
              ('{App1}','upgrade-app-1','Application one',TRUE,now(),now()),
              ('{App2}','upgrade-app-2','Application two',TRUE,now(),now());
            INSERT INTO "ApplicationMemberships" ("Id","UserId","ApplicationId","IsActive","CreatedAt","UpdatedAt") VALUES
              ('{Guid.NewGuid()}','{User1}','{App1}',TRUE,now(),now()),
              ('{Guid.NewGuid()}','{User2}','{App1}',TRUE,now(),now()),
              ('{Guid.NewGuid()}','{User1}','{App2}',TRUE,now(),now());
            INSERT INTO "Roles" ("Id","ApplicationId","Name","NormalizedName","IsActive","CreatedAt","UpdatedAt") VALUES
              ('{Role1}','{App1}','Auditors','AUDITORS',TRUE,now(),now()),
              ('{Role2}','{App2}','Auditors','AUDITORS',TRUE,now(),now());
            INSERT INTO "Permissions" ("Id","ApplicationId","Code","IsActive","CreatedAt","UpdatedAt") VALUES
              ('{LegacyAudit1}','{App1}','audit.events.read',TRUE,now(),now()),
              ('{Business1}','{App1}','orders.read',TRUE,now(),now()),
              ('{LegacyAudit2}','{App2}','audit.events.read',TRUE,now(),now());
            INSERT INTO "RolePermissions" ("Id","ApplicationId","RoleId","PermissionId","IsActive","CreatedAt","UpdatedAt") VALUES
              ('55555555-0000-0000-0000-000000000001','{App1}','{Role1}','{LegacyAudit1}',TRUE,now(),now()),
              ('55555555-0000-0000-0000-000000000002','{App1}','{Role1}','{Business1}',TRUE,now(),now()),
              ('55555555-0000-0000-0000-000000000003','{App2}','{Role2}','{LegacyAudit2}',TRUE,now(),now());
            INSERT INTO "UserRoles" ("Id","ApplicationId","UserId","RoleId","IsActive","CreatedAt","UpdatedAt") VALUES
              ('{Guid.NewGuid()}','{App1}','{User1}','{Role1}',TRUE,now(),now()),
              ('{Guid.NewGuid()}','{App2}','{User1}','{Role2}',TRUE,now(),now());
            INSERT INTO "Sessions" ("Id","UserId","ApplicationId","CreatedAt","ExpiresAt","RevokedAt") VALUES
              ('{Guid.NewGuid()}','{User1}','{App1}',now(),now() + interval '8 hours',NULL),
              ('{Guid.NewGuid()}','{User2}','{App1}',now(),now() + interval '8 hours',now());
            INSERT INTO "SecurityEvents" ("Id","EventType","Outcome","OccurredAtUtc","UserId","ApplicationId") VALUES
              ('{Guid.NewGuid()}','LoginSucceeded','Success',now(),'{User1}','{App1}'),
              ('{Guid.NewGuid()}','LoginFailed','Failure',now(),'{User2}','{App1}');
            """);
    }

    private static async Task ExecuteAsync(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }
}
