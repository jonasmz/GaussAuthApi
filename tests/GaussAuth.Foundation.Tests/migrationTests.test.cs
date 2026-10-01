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
        Assert.AreEqual(4, (await db.Database.GetAppliedMigrationsAsync()).Count());
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
            "Applications", "ApplicationMemberships", "Roles", "Permissions", "RolePermissions", "UserRoles"
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
    }
}
