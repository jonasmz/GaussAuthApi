using System.Net;
using System.Net.Http.Json;
using GaussAuth.Infrastructure.DependencyInjection;
using GaussAuth.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GaussAuth.Foundation.Tests;

/// <summary>
/// The complete migration chain against a brand-new, empty PostgreSQL 17 database (never an already-evolved one):
/// it applies in order, is idempotent, produces the critical constraints and indexes, and the application runs on it
/// (FR-007, FR-008, SC-002).
/// </summary>
[TestClass]
public sealed class ReleaseMigrationValidationTests
{
    /// <summary>The release migration chain in application order (see data-model.md → Migration Chain).</summary>
    internal static readonly string[] ReleaseChain =
    [
        "20261001012324_InitialIdentityFoundation",
        "20261001024704_AddUsersAndProfiles",
        "20261001043323_AddApplicationsAndMemberships",
        "20261001061232_AddRolesAndPermissions",
        "20261001085523_AddSessions",
        "20261001202342_AddSecurityEvents",
        "20261001232459_AddSecurityEventActor",
        "20261002000000_SeedAdministrativePermissions",
        "20261002000617_AddApplicationConsumerCredentials",
        "20261002010000_MoveAuditPermissionToAuthNamespace"
    ];

    [TestMethod]
    public async Task Migrations_apply_in_order_from_an_empty_database_and_are_idempotent()
    {
        await using var database = await ThrowawayDatabase.CreateAsync();
        Assert.AreEqual(0L, await TableCountAsync(database));

        await using var provider = MigrationServices.Build(database.ConnectionString);
        using (var scope = provider.CreateScope())
        {
            var compiled = scope.ServiceProvider.GetRequiredService<AuthenticationDbContext>().Database.GetMigrations().ToList();
            CollectionAssert.AreEqual(ReleaseChain, compiled, "The compiled migration chain differs from the documented release chain.");

            var first = await scope.ServiceProvider.GetRequiredService<DatabaseMigrator>().MigrateAsync(CancellationToken.None);
            Assert.IsTrue(first.Succeeded, $"{first.FailureCategory} {first.FailedMigration} {first.FailureCode}");
            CollectionAssert.AreEqual(ReleaseChain, first.AppliedMigrations.ToList());
        }

        CollectionAssert.AreEqual(ReleaseChain, (await DatabaseSchema.AppliedMigrationsAsync(database.ConnectionString)).ToList());

        using (var scope = provider.CreateScope())
        {
            var again = await scope.ServiceProvider.GetRequiredService<DatabaseMigrator>().MigrateAsync(CancellationToken.None);
            Assert.IsTrue(again.Succeeded);
            Assert.AreEqual(0, again.AppliedMigrations.Count, "Re-running must be a no-op.");
            Assert.IsEmpty(await scope.ServiceProvider.GetRequiredService<AuthenticationDbContext>().Database.GetPendingMigrationsAsync());
        }
    }

    [TestMethod]
    public async Task Critical_constraints_and_indexes_exist_after_migrating_from_zero()
    {
        await using var database = await ThrowawayDatabase.CreateAsync();
        await MigrationServices.MigrateAsync(database.ConnectionString);

        var unique = await DatabaseSchema.UniqueIndexesAsync(database.ConnectionString);
        foreach (var expected in new[]
        {
            "Users(NormalizedEmail)",
            "AspNetUsers(NormalizedEmail)",
            "AspNetUsers(NormalizedUserName)",
            "Applications(Code)",
            "ApplicationMemberships(UserId,ApplicationId)",
            "Roles(ApplicationId,NormalizedName)",
            "Permissions(ApplicationId,Code)",
            "Roles(Id,ApplicationId)",
            "Permissions(Id,ApplicationId)",
            "RolePermissions(RoleId,PermissionId)",
            "UserRoles(UserId,RoleId,ApplicationId)",
            "ApplicationConsumerCredentials(ApplicationId)"
        })
        {
            Assert.Contains(expected, unique, $"Missing unique index {expected}.");
        }

        var indexes = await DatabaseSchema.IndexNamesAsync(database.ConnectionString);
        foreach (var expected in new[]
        {
            "IX_Sessions_UserId_ApplicationId",
            "IX_SecurityEvents_OccurredAtUtc_Id",
            "IX_SecurityEvents_ActorUserId_OccurredAtUtc_Id",
            "IX_SecurityEvents_ApplicationId_OccurredAtUtc_Id",
            "IX_SecurityEvents_EventType_OccurredAtUtc_Id",
            "IX_SecurityEvents_SessionId_OccurredAtUtc_Id",
            "IX_SecurityEvents_UserId_OccurredAtUtc_Id"
        })
        {
            Assert.Contains(expected, indexes, $"Missing index {expected}.");
        }

        // Cross-application integrity: role/permission assignments reference (Id, ApplicationId) pairs, so an assignment can
        // never join a role and a permission (or a user's membership) from different Applications.
        Assert.IsGreaterThanOrEqualTo(2, await DatabaseSchema.ForeignKeysWithColumnCountAsync(database.ConnectionString, "RolePermissions", 2));
        Assert.IsGreaterThanOrEqualTo(2, await DatabaseSchema.ForeignKeysWithColumnCountAsync(database.ConnectionString, "UserRoles", 2));
    }

    [TestMethod]
    public async Task The_application_starts_and_serves_requests_against_the_migrated_schema()
    {
        await using var database = await ThrowawayDatabase.CreateAsync();
        await MigrationServices.MigrateAsync(database.ConnectionString);

        var original = Environment.GetEnvironmentVariable("ConnectionStrings__AuthenticationDatabase");
        Environment.SetEnvironmentVariable("ConnectionStrings__AuthenticationDatabase", database.ConnectionString);
        try
        {
            using var factory = new WebApplicationFactory<Program>().WithGlobalAdministrators();
            using var administrator = await factory.CreateAdminClientAsync();

            using var created = await administrator.Client.PostAsJsonAsync("/admin/applications", new { code = $"fresh-{Guid.NewGuid():N}", name = "Fresh schema" });
            using var listed = await administrator.Client.GetAsync("/admin/applications");

            Assert.AreEqual(HttpStatusCode.Created, created.StatusCode);
            Assert.AreEqual(HttpStatusCode.OK, listed.StatusCode);
            // Creating an Application seeds its administrative permissions: proves the seeded schema and constraints work end to end.
            Assert.IsGreaterThanOrEqualTo(18L, await DatabaseSchema.CountAsync(database.ConnectionString, "Permissions", "\"Code\" LIKE 'auth.%'"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("ConnectionStrings__AuthenticationDatabase", original);
        }
    }

    private static async Task<long> TableCountAsync(ThrowawayDatabase database)
    {
        await using var connection = new Npgsql.NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT count(*) FROM information_schema.tables WHERE table_schema = 'public'";
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }
}
