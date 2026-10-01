using GaussAuth.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GaussAuth.Foundation.Tests;

[TestClass]
public sealed class UsersSchemaMigrationTests
{
    [TestMethod]
    public async Task Users_and_profiles_schema_applies_idempotently_and_stays_scoped()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuthenticationDbContext>();

        await db.Database.MigrateAsync();
        await db.Database.MigrateAsync();
        Assert.AreEqual(5, (await db.Database.GetAppliedMigrationsAsync()).Count());
        Assert.IsEmpty(await db.Database.GetPendingMigrationsAsync());

        var connection = db.Database.GetDbConnection();
        await connection.OpenAsync();

        var tables = new HashSet<string>(StringComparer.Ordinal);
        await using (var tableCommand = connection.CreateCommand())
        {
            tableCommand.CommandText =
                "SELECT table_name FROM information_schema.tables WHERE table_schema = 'public' AND table_type = 'BASE TABLE'";
            await using var reader = await tableCommand.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                tables.Add(reader.GetString(0));
            }
        }

        var expectedTables = new[]
        {
            "AspNetUsers", "AspNetUserClaims", "AspNetUserLogins", "AspNetUserTokens",
            "__EFMigrationsHistory", "Users", "UserProfiles", "Applications", "ApplicationMemberships",
            "Roles", "Permissions", "RolePermissions", "UserRoles", "Sessions"
        };
        CollectionAssert.AreEquivalent(expectedTables, tables.ToArray());

        var userIndexes = new List<string>();
        await using (var indexCommand = connection.CreateCommand())
        {
            indexCommand.CommandText =
                "SELECT indexdef FROM pg_indexes WHERE schemaname = 'public' AND tablename = 'Users'";
            await using var reader = await indexCommand.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                userIndexes.Add(reader.GetString(0));
            }
        }

        Assert.IsTrue(userIndexes.Any(index =>
            index.Contains("UNIQUE", StringComparison.Ordinal) &&
            index.Contains("NormalizedEmail", StringComparison.Ordinal)));

        var foreignKeys = new List<(string Table, string ReferencedTable)>();
        await using (var fkCommand = connection.CreateCommand())
        {
            fkCommand.CommandText = """
                SELECT tc.table_name, ccu.table_name AS referenced_table
                FROM information_schema.table_constraints tc
                JOIN information_schema.constraint_column_usage ccu
                    ON tc.constraint_name = ccu.constraint_name
                WHERE tc.constraint_type = 'FOREIGN KEY'
                  AND tc.table_schema = 'public'
                  AND tc.table_name IN ('Users', 'UserProfiles', 'ApplicationMemberships', 'Roles', 'Permissions', 'RolePermissions', 'UserRoles')
                """;
            await using var reader = await fkCommand.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                foreignKeys.Add((reader.GetString(0), reader.GetString(1)));
            }
        }

        Assert.IsTrue(foreignKeys.Contains(("Users", "AspNetUsers")));
        Assert.IsTrue(foreignKeys.Contains(("UserProfiles", "Users")));
        Assert.IsTrue(foreignKeys.Contains(("ApplicationMemberships", "Users")));
        Assert.IsTrue(foreignKeys.Contains(("ApplicationMemberships", "Applications")));
        Assert.IsTrue(foreignKeys.Contains(("Roles", "Applications")));
        Assert.IsTrue(foreignKeys.Contains(("Permissions", "Applications")));
        Assert.IsTrue(foreignKeys.Contains(("RolePermissions", "Roles")));
        Assert.IsTrue(foreignKeys.Contains(("RolePermissions", "Permissions")));
        Assert.IsTrue(foreignKeys.Contains(("UserRoles", "ApplicationMemberships")));
        Assert.IsTrue(foreignKeys.Contains(("UserRoles", "Roles")));
    }
}
