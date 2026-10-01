using GaussAuth.Application.Administration.Authorization;
using GaussAuth.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace GaussAuth.Foundation.Tests;

[TestClass]
public sealed class AdministrationMigrationTests
{
    private const string PreviousMigration = "20261001232459_AddSecurityEventActor";

    [TestMethod]
    public async Task New_application_receives_exactly_the_seeded_permissions_without_roles_or_assignments()
    {
        using var factory = new WebApplicationFactory<Program>().WithGlobalAdministrators();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuthenticationDbContext>();
        await db.Database.MigrateAsync();

        var (applicationId, _) = await factory.CreateApplicationAsync();

        var permissions = await db.Permissions.AsNoTracking().Where(item => item.ApplicationId == applicationId).ToListAsync();
        CollectionAssert.AreEquivalent(AdministrativePermissionCatalog.SeededPermissions.Select(item => item.Code).ToArray(), permissions.Select(item => item.Code).ToArray());
        Assert.AreEqual(9, permissions.Count);
        Assert.IsTrue(permissions.All(item => item.IsActive));
        foreach (var (code, description) in AdministrativePermissionCatalog.SeededPermissions)
            Assert.AreEqual(description, permissions.Single(item => item.Code == code).Description);
        Assert.AreEqual(0, await db.Roles.CountAsync(item => item.ApplicationId == applicationId));
        Assert.AreEqual(0, await db.UserRoles.CountAsync(item => item.ApplicationId == applicationId));
    }

    [TestMethod]
    public async Task Seeding_migration_is_idempotent_and_reversible_without_duplicates()
    {
        using var factory = new WebApplicationFactory<Program>().WithGlobalAdministrators();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuthenticationDbContext>();
        await db.Database.MigrateAsync();
        var (applicationId, _) = await factory.CreateApplicationAsync();
        var migrator = db.GetService<IMigrator>();

        await RollBackAsync(db, migrator);
        Assert.AreEqual(0, await CountAsync(db, applicationId));
        await migrator.MigrateAsync();
        Assert.AreEqual(9, await CountAsync(db, applicationId));
        await db.Database.ExecuteSqlRawAsync("""
            INSERT INTO "Permissions" ("Id", "ApplicationId", "Code", "Description", "IsActive", "CreatedAt", "UpdatedAt")
            SELECT gen_random_uuid(), a."Id", 'auth.roles.read', 'x', TRUE, now(), now() FROM "Applications" a
            ON CONFLICT ("ApplicationId", "Code") DO NOTHING
            """);
        Assert.AreEqual(9, await CountAsync(db, applicationId));
        Assert.IsEmpty(await db.Database.GetPendingMigrationsAsync());
    }

    [TestMethod]
    public async Task Seeding_migration_fails_closed_when_a_reserved_prefix_permission_already_exists()
    {
        using var factory = new WebApplicationFactory<Program>().WithGlobalAdministrators();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuthenticationDbContext>();
        await db.Database.MigrateAsync();
        var (applicationId, _) = await factory.CreateApplicationAsync();
        var migrator = db.GetService<IMigrator>();

        await RollBackAsync(db, migrator);
        var conflictingId = Guid.NewGuid();
        try
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "Permissions" ("Id", "ApplicationId", "Code", "Description", "IsActive", "CreatedAt", "UpdatedAt")
                VALUES ({conflictingId}, {applicationId}, 'auth.preexisting', NULL, TRUE, now(), now())
                """);
            var failure = await Assert.ThrowsAsync<Npgsql.PostgresException>(() => migrator.MigrateAsync());
            StringAssert.Contains(failure.MessageText, "reserved auth. prefix");
            Assert.AreEqual(0, await CountAsync(db, applicationId, "auth.roles.read"));
        }
        finally
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"""DELETE FROM "Permissions" WHERE "Id" = {conflictingId}""");
            await migrator.MigrateAsync();
        }

        Assert.AreEqual(9, await CountAsync(db, applicationId));
    }

    /// <summary>
    /// Rolls the seeding migration back. The Down step keeps seeded permissions that roles still use, and the Up step refuses
    /// to run while any <c>auth.</c> permission exists, so assignments left by other tests in the shared test database are cleared first.
    /// </summary>
    private static async Task RollBackAsync(AuthenticationDbContext db, IMigrator migrator)
    {
        await db.Database.ExecuteSqlRawAsync("""
            DELETE FROM "RolePermissions" WHERE "PermissionId" IN (SELECT "Id" FROM "Permissions" WHERE "Code" LIKE 'auth.%')
            """);
        await migrator.MigrateAsync(PreviousMigration);
    }

    private static Task<int> CountAsync(AuthenticationDbContext db, Guid applicationId, string? code = null) =>
        db.Permissions.AsNoTracking().CountAsync(item => item.ApplicationId == applicationId && item.Code.StartsWith("auth.") && (code == null || item.Code == code));
}
