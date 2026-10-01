using GaussAuth.Application.Administration.Authorization;
using GaussAuth.Infrastructure.Administration;
using GaussAuth.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GaussAuth.Foundation.Tests;

[TestClass]
public sealed class AdministrationTests
{
    [TestMethod]
    public void Global_administrator_policy_reads_only_the_configured_list()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var policy = new ConfiguredGlobalAdministratorPolicy(Configuration(new()
        {
            ["Administration:GlobalAdministratorUserIds:0"] = first.ToString(),
            ["Administration:GlobalAdministratorUserIds:1"] = second.ToString(),
            ["Administration:GlobalAdministratorUserIds:2"] = "  ",
        }));

        Assert.IsTrue(policy.IsGlobalAdministrator(first));
        Assert.IsTrue(policy.IsGlobalAdministrator(second));
        Assert.IsFalse(policy.IsGlobalAdministrator(Guid.NewGuid()));
        Assert.IsFalse(new ConfiguredGlobalAdministratorPolicy(Configuration(new())).IsGlobalAdministrator(first));
    }

    [TestMethod]
    public void Global_administrator_policy_rejects_invalid_entries_and_the_legacy_key()
    {
        Assert.Throws<InvalidOperationException>(() => new ConfiguredGlobalAdministratorPolicy(Configuration(new()
        {
            ["Administration:GlobalAdministratorUserIds:0"] = "not-a-guid",
        })));
        Assert.Throws<InvalidOperationException>(() => new ConfiguredGlobalAdministratorPolicy(Configuration(new()
        {
            ["Administration:GlobalAdministratorUserIds:0"] = Guid.Empty.ToString(),
        })));
        var legacy = Assert.Throws<InvalidOperationException>(() => new ConfiguredGlobalAdministratorPolicy(Configuration(new()
        {
            ["SecurityAudit:GlobalReviewerUserId"] = Guid.NewGuid().ToString(),
        })));
        StringAssert.Contains(legacy.Message, "Administration:GlobalAdministratorUserIds");
    }

    [TestMethod]
    public async Task Authorizer_distinguishes_unauthenticated_forbidden_and_global_callers()
    {
        var policy = new TestGlobalAdministratorPolicy();
        using var factory = new WebApplicationFactory<Program>().WithGlobalAdministrators(policy);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuthenticationDbContext>();
        await db.Database.MigrateAsync();
        var authorizer = scope.ServiceProvider.GetRequiredService<AdministrativeAuthorizer>();

        using var global = await factory.CreateGlobalAdministratorAsync(policy);
        var (applicationId, applicationCode) = await factory.CreateApplicationAsync();
        using var ordinary = await factory.CreateApplicationAdministratorAsync(applicationId, applicationCode);

        var missing = await authorizer.AuthorizeAsync(null, AdministrativeScope.Global, null, null, CancellationToken.None);
        var malformed = await authorizer.AuthorizeAsync("not-a-credential", AdministrativeScope.Global, null, null, CancellationToken.None);
        Assert.AreEqual(AdministrativeAuthorizationOutcome.Unauthenticated, missing.Outcome);
        Assert.AreEqual(AdministrativeAuthorizationOutcome.Unauthenticated, malformed.Outcome);

        var globalOnGlobal = await authorizer.AuthorizeAsync(global.AccessToken, AdministrativeScope.Global, null, null, CancellationToken.None);
        var globalOnApplication = await authorizer.AuthorizeAsync(global.AccessToken, AdministrativeScope.Application,
            AdministrativePermissionCatalog.RolesManage, Guid.NewGuid(), CancellationToken.None);
        Assert.IsTrue(globalOnGlobal.IsAuthorized && globalOnGlobal.IsGlobal);
        Assert.IsTrue(globalOnApplication.IsAuthorized && globalOnApplication.IsGlobal);
        Assert.AreEqual(global.UserId, globalOnGlobal.ActorUserId);

        var forbiddenGlobal = await authorizer.AuthorizeAsync(ordinary.AccessToken, AdministrativeScope.Global, null, null, CancellationToken.None);
        var forbiddenApplication = await authorizer.AuthorizeAsync(ordinary.AccessToken, AdministrativeScope.Application,
            AdministrativePermissionCatalog.RolesManage, applicationId, CancellationToken.None);
        Assert.AreEqual(AdministrativeAuthorizationOutcome.Forbidden, forbiddenGlobal.Outcome);
        Assert.AreEqual(AdministrativeAuthorizationOutcome.Forbidden, forbiddenApplication.Outcome);

        var denials = await db.SecurityEvents.Where(item => item.EventType == "administration.access.denied" && item.ActorUserId == ordinary.UserId)
            .OrderBy(item => item.OccurredAtUtc).ToListAsync();
        Assert.AreEqual(2, denials.Count);
        Assert.AreEqual("rejected", denials[0].Outcome);
        Assert.AreEqual(AdministrativeAuthorizer.GlobalRequirement, denials[0].Reason);
        Assert.AreEqual(AdministrativePermissionCatalog.RolesManage, denials[1].Reason);
        Assert.AreEqual(applicationId, denials[1].ApplicationId);
        Assert.AreEqual(0, await db.SecurityEvents.CountAsync(item => item.EventType == "administration.access.denied" && item.ActorUserId == null));
    }

    [TestMethod]
    public void Permission_catalog_reserves_the_prefix_and_lists_the_seeded_codes()
    {
        Assert.IsTrue(AdministrativePermissionCatalog.IsReservedPrefix("auth.anything"));
        Assert.IsTrue(AdministrativePermissionCatalog.IsReservedPrefix("AUTH.Anything"));
        Assert.IsFalse(AdministrativePermissionCatalog.IsReservedPrefix("roles.manage"));
        Assert.IsTrue(AdministrativePermissionCatalog.IsPlatformPermission("auth.roles.manage"));
        Assert.IsTrue(AdministrativePermissionCatalog.IsPlatformPermission("auth.consumer-secrets.rotate"));
        Assert.IsFalse(AdministrativePermissionCatalog.IsPlatformPermission("auth.custom"));
        Assert.AreEqual(9, AdministrativePermissionCatalog.SeededPermissions.Count);
        Assert.IsTrue(AdministrativePermissionCatalog.SeededPermissions.All(item => item.Code.StartsWith("auth.", StringComparison.Ordinal)));
        Assert.IsFalse(AdministrativePermissionCatalog.SeededPermissions.Any(item => item.Code.StartsWith("auth.users.", StringComparison.Ordinal)));
    }

    private static IConfiguration Configuration(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();
}
