using GaussAuth.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace GaussAuth.Foundation.Tests;

[TestClass]
public sealed class MigrationTests
{
    [TestMethod]
    public async Task Initial_identity_schema_applies_idempotently_to_postgresql_17()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var scope = factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<AuthenticationDbContext>();

        var userStore = services.GetRequiredService<IUserStore<IdentityUser<Guid>>>();
        Assert.IsTrue(userStore is IUserPasswordStore<IdentityUser<Guid>>);
        Assert.IsTrue(userStore is IUserLockoutStore<IdentityUser<Guid>>);
        Assert.IsNotNull(services.GetService<UserManager<IdentityUser<Guid>>>());
        Assert.IsNull(services.GetService<IRoleStore<IdentityRole<Guid>>>());
        Assert.AreEqual(IdentitySchemaVersions.Version2,
            services.GetRequiredService<IOptions<IdentityOptions>>().Value.Stores.SchemaVersion);

        await db.Database.MigrateAsync();
        await db.Database.MigrateAsync();
        Assert.AreEqual(10, (await db.Database.GetAppliedMigrationsAsync()).Count());
        Assert.IsEmpty(await db.Database.GetPendingMigrationsAsync());

        var connection = db.Database.GetDbConnection();
        await connection.OpenAsync();

        await using (var versionCommand = connection.CreateCommand())
        {
            versionCommand.CommandText = "SELECT current_setting('server_version_num')::integer";
            var version = Convert.ToInt32(await versionCommand.ExecuteScalarAsync());
            Assert.AreEqual(17, version / 10000);
        }

        var tables = new HashSet<string>(StringComparer.Ordinal);
        await using (var tableCommand = connection.CreateCommand())
        {
            tableCommand.CommandText = "SELECT table_name FROM information_schema.tables WHERE table_schema = 'public' AND table_type = 'BASE TABLE'";
            await using var reader = await tableCommand.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                tables.Add(reader.GetString(0));
            }
        }

        var expectedTables = new[]
        {
            "AspNetUsers", "AspNetUserClaims", "AspNetUserLogins",
            "AspNetUserTokens", "__EFMigrationsHistory", "Users", "UserProfiles",
            "Applications", "ApplicationMemberships", "Roles", "Permissions", "RolePermissions", "UserRoles", "Sessions", "SecurityEvents", "ApplicationConsumerCredentials"
        };
        CollectionAssert.AreEquivalent(expectedTables, tables.ToArray());

        var indexes = new List<string>();
        await using (var indexCommand = connection.CreateCommand())
        {
            indexCommand.CommandText = "SELECT indexdef FROM pg_indexes WHERE schemaname = 'public' AND tablename = 'AspNetUsers'";
            await using var reader = await indexCommand.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                indexes.Add(reader.GetString(0));
            }
        }

        Assert.IsTrue(indexes.Any(index => index.Contains("UNIQUE", StringComparison.Ordinal) &&
                                           index.Contains("NormalizedEmail", StringComparison.Ordinal)));
        Assert.IsTrue(indexes.Any(index => index.Contains("UNIQUE", StringComparison.Ordinal) &&
                                           index.Contains("NormalizedUserName", StringComparison.Ordinal)));

        var securityEventIndexes = new List<string>();
        await using (var securityIndexCommand = connection.CreateCommand())
        {
            securityIndexCommand.CommandText = "SELECT indexname FROM pg_indexes WHERE schemaname = 'public' AND tablename = 'SecurityEvents'";
            await using var reader = await securityIndexCommand.ExecuteReaderAsync();
            while (await reader.ReadAsync()) securityEventIndexes.Add(reader.GetString(0));
        }
        CollectionAssert.IsSubsetOf(new[]
        {
            "IX_SecurityEvents_OccurredAtUtc_Id", "IX_SecurityEvents_ApplicationId_OccurredAtUtc_Id",
            "IX_SecurityEvents_UserId_OccurredAtUtc_Id", "IX_SecurityEvents_SessionId_OccurredAtUtc_Id",
            "IX_SecurityEvents_EventType_OccurredAtUtc_Id"
        }, securityEventIndexes);

        var columns = new Dictionary<string, (string Type, string Nullable)>();
        await using (var columnCommand = connection.CreateCommand())
        {
            columnCommand.CommandText = "SELECT column_name, data_type, is_nullable FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'Sessions'";
            await using var reader = await columnCommand.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                columns[reader.GetString(0)] = (reader.GetString(1), reader.GetString(2));
            }
        }

        CollectionAssert.AreEquivalent(new[] { "Id", "UserId", "ApplicationId", "CreatedAt", "ExpiresAt", "RevokedAt" }, columns.Keys.ToArray());
        Assert.AreEqual(("uuid", "NO"), columns["Id"]);
        Assert.AreEqual(("uuid", "NO"), columns["UserId"]);
        Assert.AreEqual(("uuid", "NO"), columns["ApplicationId"]);
        Assert.AreEqual(("timestamp with time zone", "NO"), columns["CreatedAt"]);
        Assert.AreEqual(("timestamp with time zone", "NO"), columns["ExpiresAt"]);
        Assert.AreEqual(("timestamp with time zone", "YES"), columns["RevokedAt"]);

        await using (var constraintCommand = connection.CreateCommand())
        {
            constraintCommand.CommandText = "SELECT count(*) FROM information_schema.table_constraints WHERE table_name = 'Sessions' AND constraint_name = 'FK_Sessions_ApplicationMemberships_UserId_ApplicationId' AND constraint_type = 'FOREIGN KEY'";
            Assert.AreEqual(1, Convert.ToInt32(await constraintCommand.ExecuteScalarAsync()));
        }

        await using (var sessionIndexCommand = connection.CreateCommand())
        {
            sessionIndexCommand.CommandText = "SELECT count(*) FROM pg_indexes WHERE schemaname = 'public' AND tablename = 'Sessions' AND indexname = 'IX_Sessions_UserId_ApplicationId'";
            Assert.AreEqual(1, Convert.ToInt32(await sessionIndexCommand.ExecuteScalarAsync()));
        }
    }

    [TestMethod]
    public async Task Avatar_reference_column_is_nullable_text_and_needs_no_additional_migration()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuthenticationDbContext>();
        await db.Database.MigrateAsync();
        Assert.IsEmpty(await db.Database.GetPendingMigrationsAsync());

        var connection = db.Database.GetDbConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT data_type, is_nullable FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'UserProfiles' AND column_name = 'AvatarReference'";
        await using var reader = await command.ExecuteReaderAsync();
        Assert.IsTrue(await reader.ReadAsync());
        Assert.AreEqual("text", reader.GetString(0));
        Assert.AreEqual("YES", reader.GetString(1));
    }
}
