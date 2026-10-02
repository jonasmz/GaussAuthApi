using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GaussAuth.Application.Administration.Authorization;
using GaussAuth.Application.Permissions;
using GaussAuth.Application.Permissions.Ports;
using GaussAuth.Application.Roles;
using GaussAuth.Application.Authorization;
using GaussAuth.Application.Sessions;
using GaussAuth.Application.Users.DeactivateUser;
using GaussAuth.Infrastructure.Administration;
using GaussAuth.Infrastructure.Configuration;
using GaussAuth.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Identity;
using GaussAuth.Application.Security;
using GaussAuth.Application.Security.Ports;
using GaussAuth.Application.Applications;

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
        var invalid = Assert.ThrowsExactly<StartupConfigurationException>(() => new ConfiguredGlobalAdministratorPolicy(Configuration(new()
        {
            ["Administration:GlobalAdministratorUserIds:0"] = "not-a-guid",
        })));
        Assert.AreEqual("Administration:GlobalAdministratorUserIds", invalid.Setting);
        Assert.DoesNotContain("not-a-guid", invalid.Message);
        var empty = Assert.ThrowsExactly<StartupConfigurationException>(() => new ConfiguredGlobalAdministratorPolicy(Configuration(new()
        {
            ["Administration:GlobalAdministratorUserIds:0"] = Guid.Empty.ToString(),
        })));
        Assert.AreEqual("Administration:GlobalAdministratorUserIds", empty.Setting);
        var legacy = Assert.ThrowsExactly<StartupConfigurationException>(() => new ConfiguredGlobalAdministratorPolicy(Configuration(new()
        {
            ["SecurityAudit:GlobalReviewerUserId"] = Guid.NewGuid().ToString(),
        })));
        Assert.AreEqual("SecurityAudit:GlobalReviewerUserId", legacy.Setting);
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

    [TestMethod]
    public async Task Admin_routes_reject_unauthenticated_revoked_expired_and_ineligible_callers()
    {
        var policy = new TestGlobalAdministratorPolicy();
        var time = new MutableTimeProvider();
        using var factory = await MigratedFactoryAsync(policy, time);
        using var anonymous = factory.CreateClient();
        using var none = await anonymous.GetAsync("/admin/applications");
        Assert.AreEqual(HttpStatusCode.Unauthorized, none.StatusCode);
        anonymous.DefaultRequestHeaders.Authorization = new("Bearer", "garbage");
        using var malformed = await anonymous.GetAsync("/admin/applications");
        Assert.AreEqual(HttpStatusCode.Unauthorized, malformed.StatusCode);

        using var active = await factory.CreateGlobalAdministratorAsync(policy);
        using var ok = await active.Client.GetAsync("/admin/applications");
        Assert.AreEqual(HttpStatusCode.OK, ok.StatusCode);

        using var revoked = await factory.CreateGlobalAdministratorAsync(policy);
        using (var scope = factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<SessionService>().RevokeAsync(revoked.SessionId, CancellationToken.None);
        using var afterRevoke = await revoked.Client.GetAsync("/admin/applications");
        Assert.AreEqual(HttpStatusCode.Unauthorized, afterRevoke.StatusCode);

        using var deactivated = await factory.CreateGlobalAdministratorAsync(policy);
        using (var scope = factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<DeactivateUserHandler>().HandleAsync(deactivated.UserId, CancellationToken.None);
        using var afterDeactivate = await deactivated.Client.GetAsync("/admin/applications");
        Assert.AreEqual(HttpStatusCode.Unauthorized, afterDeactivate.StatusCode);
        // An inactive user cannot obtain a new session even while listed as a global administrator.
        await Assert.ThrowsAsync<InvalidOperationException>(() => factory.SignInAsync(deactivated.Email, deactivated.ApplicationId, deactivated.ApplicationCode));

        time.Advance(TimeSpan.FromDays(2));
        using var afterExpiry = await active.Client.GetAsync("/admin/applications");
        Assert.AreEqual(HttpStatusCode.Unauthorized, afterExpiry.StatusCode);
    }

    [TestMethod]
    public async Task Application_administrator_is_scoped_and_forbidden_responses_do_not_reveal_existence()
    {
        var policy = new TestGlobalAdministratorPolicy();
        using var factory = await MigratedFactoryAsync(policy);
        var (applicationA, codeA) = await factory.CreateApplicationAsync();
        var (applicationB, _) = await factory.CreateApplicationAsync();
        using var adminA = await factory.CreateApplicationAdministratorAsync(applicationA, codeA,
            AdministrativePermissionCatalog.RolesRead, AdministrativePermissionCatalog.RolesManage);

        using var created = await adminA.Client.PostAsJsonAsync($"/admin/applications/{applicationA}/roles", new { name = "Support" });
        using var listed = await adminA.Client.GetAsync($"/admin/applications/{applicationA}/roles");
        Assert.AreEqual(HttpStatusCode.Created, created.StatusCode);
        Assert.AreEqual(HttpStatusCode.OK, listed.StatusCode);

        using var otherExisting = await adminA.Client.GetAsync($"/admin/applications/{applicationB}/roles");
        using var otherMissing = await adminA.Client.GetAsync($"/admin/applications/{Guid.NewGuid()}/roles");
        Assert.AreEqual(HttpStatusCode.Forbidden, otherExisting.StatusCode);
        Assert.AreEqual(HttpStatusCode.Forbidden, otherMissing.StatusCode);
        Assert.AreEqual(await ProblemShapeAsync(otherExisting), await ProblemShapeAsync(otherMissing));

        // A permission held in the application does not extend to operations that need a different one.
        using var membershipsRead = await adminA.Client.GetAsync($"/admin/applications/{applicationA}/memberships");
        using var permissionsRead = await adminA.Client.GetAsync($"/admin/applications/{applicationA}/permissions");
        Assert.AreEqual(HttpStatusCode.Forbidden, membershipsRead.StatusCode);
        Assert.AreEqual(HttpStatusCode.Forbidden, permissionsRead.StatusCode);

        // Global routes are not reachable with application permissions.
        using var applications = await adminA.Client.GetAsync("/admin/applications");
        using var user = await adminA.Client.GetAsync($"/admin/users/{adminA.UserId}");
        using var createApplication = await adminA.Client.PostAsJsonAsync("/admin/applications", new { code = $"app-{Guid.NewGuid():N}", name = "X" });
        using var userMemberships = await adminA.Client.GetAsync($"/admin/users/{adminA.UserId}/memberships");
        Assert.AreEqual(HttpStatusCode.Forbidden, applications.StatusCode);
        Assert.AreEqual(HttpStatusCode.Forbidden, user.StatusCode);
        Assert.AreEqual(HttpStatusCode.Forbidden, createApplication.StatusCode);
        Assert.AreEqual(HttpStatusCode.Forbidden, userMemberships.StatusCode);
    }

    [TestMethod]
    public async Task Global_administrator_reaches_any_application_without_gaining_effective_permissions()
    {
        var policy = new TestGlobalAdministratorPolicy();
        using var factory = await MigratedFactoryAsync(policy);
        using var global = await factory.CreateGlobalAdministratorAsync(policy);
        var (otherApplication, _) = await factory.CreateApplicationAsync();

        using var roles = await global.Client.PostAsJsonAsync($"/admin/applications/{otherApplication}/roles", new { name = "Ops" });
        using var users = await global.Client.GetAsync("/admin/applications");
        using var own = await global.Client.GetAsync($"/admin/applications/{global.ApplicationId}/users/{global.UserId}/effective-permissions");
        Assert.AreEqual(HttpStatusCode.Created, roles.StatusCode);
        Assert.AreEqual(HttpStatusCode.OK, users.StatusCode);
        Assert.AreEqual(HttpStatusCode.OK, own.StatusCode);
        Assert.AreEqual(0, JsonDocument.Parse(await own.Content.ReadAsStringAsync()).RootElement.GetProperty("items").GetArrayLength());

        using var scope = factory.Services.CreateScope();
        var effective = await scope.ServiceProvider.GetRequiredService<EffectivePermissionService>().GetAsync(global.ApplicationId, global.UserId, CancellationToken.None);
        Assert.AreEqual(0, effective.Items.Count);
    }

    [TestMethod]
    public async Task Business_permissions_and_roles_named_like_administrative_ones_grant_nothing()
    {
        var policy = new TestGlobalAdministratorPolicy();
        using var factory = await MigratedFactoryAsync(policy);
        var (applicationId, code) = await factory.CreateApplicationAsync();
        using var caller = await factory.CreateApplicationAdministratorAsync(applicationId, code);
        using (var scope = factory.Services.CreateScope())
        {
            var provider = scope.ServiceProvider;
            var role = (await provider.GetRequiredService<RoleService>().CreateAsync(applicationId, "Administrator", null, CancellationToken.None)).Role!;
            var permission = (await provider.GetRequiredService<PermissionService>().CreateAsync(applicationId, "roles.manage", null, CancellationToken.None)).Permission!;
            Assert.IsNotNull((await provider.GetRequiredService<RolePermissionService>().AssignAsync(applicationId, role.Id, permission.Id, CancellationToken.None)).RolePermission);
            Assert.IsNotNull((await provider.GetRequiredService<UserRoleService>().AssignAsync(applicationId, caller.UserId, role.Id, CancellationToken.None)).UserRole);
        }

        using var response = await caller.Client.PostAsJsonAsync($"/admin/applications/{applicationId}/roles", new { name = "Other" });
        using var list = await caller.Client.GetAsync($"/admin/applications/{applicationId}/roles");
        Assert.AreEqual(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.AreEqual(HttpStatusCode.Forbidden, list.StatusCode);
    }

    [TestMethod]
    public async Task Legacy_management_routes_no_longer_exist()
    {
        var policy = new TestGlobalAdministratorPolicy();
        using var factory = await MigratedFactoryAsync(policy);
        using var global = await factory.CreateGlobalAdministratorAsync(policy);
        var id = global.ApplicationId;
        var user = global.UserId;
        var requests = new (HttpMethod Method, string Path)[]
        {
            (HttpMethod.Post, "/users"), (HttpMethod.Get, $"/users/{user}"), (HttpMethod.Put, $"/users/{user}/profile"),
            (HttpMethod.Post, $"/users/{user}/activate"), (HttpMethod.Post, $"/users/{user}/deactivate"), (HttpMethod.Get, $"/users/{user}/memberships"),
            (HttpMethod.Post, "/applications"), (HttpMethod.Get, "/applications"), (HttpMethod.Get, $"/applications/{id}"),
            (HttpMethod.Get, "/applications/by-code/x"), (HttpMethod.Post, $"/applications/{id}/activate"),
            (HttpMethod.Get, $"/applications/{id}/memberships"), (HttpMethod.Get, $"/applications/{id}/roles"),
            (HttpMethod.Get, $"/applications/{id}/permissions"), (HttpMethod.Get, $"/applications/{id}/users/{user}/effective-permissions"),
        };
        foreach (var (method, path) in requests)
        {
            using var request = new HttpRequestMessage(method, path);
            request.Headers.Authorization = global.Client.DefaultRequestHeaders.Authorization;
            using var response = await global.Client.SendAsync(request);
            Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode, $"{method} {path}");
        }
    }

    [TestMethod]
    public async Task Reserved_prefix_and_platform_permissions_cannot_be_managed_through_permission_routes()
    {
        var policy = new TestGlobalAdministratorPolicy();
        using var factory = await MigratedFactoryAsync(policy);
        using var global = await factory.CreateGlobalAdministratorAsync(policy);
        var (applicationId, _) = await factory.CreateApplicationAsync();

        using var reserved = await global.Client.PostAsJsonAsync($"/admin/applications/{applicationId}/permissions", new { code = "auth.custom" });
        Assert.AreEqual(HttpStatusCode.BadRequest, reserved.StatusCode);
        using var business = await global.Client.PostAsJsonAsync($"/admin/applications/{applicationId}/permissions", new { code = "roles.manage" });
        Assert.AreEqual(HttpStatusCode.Created, business.StatusCode);

        Guid platformId;
        using (var scope = factory.Services.CreateScope())
            platformId = (await scope.ServiceProvider.GetRequiredService<IPermissionRepository>()
                .GetByCodeAsync(applicationId, AdministrativePermissionCatalog.RolesManage, CancellationToken.None))!.Id;
        using var deactivate = await global.Client.PostAsync($"/admin/applications/{applicationId}/permissions/{platformId}/deactivate", null);
        using var activate = await global.Client.PostAsync($"/admin/applications/{applicationId}/permissions/{platformId}/activate", null);
        using var describe = await global.Client.PutAsJsonAsync($"/admin/applications/{applicationId}/permissions/{platformId}/description", new { description = "changed" });
        Assert.AreEqual(HttpStatusCode.Conflict, deactivate.StatusCode);
        Assert.AreEqual(HttpStatusCode.Conflict, activate.StatusCode);
        Assert.AreEqual(HttpStatusCode.Conflict, describe.StatusCode);
        using var read = await global.Client.GetAsync($"/admin/applications/{applicationId}/permissions/{platformId}");
        Assert.IsTrue(JsonDocument.Parse(await read.Content.ReadAsStringAsync()).RootElement.GetProperty("isActive").GetBoolean());
    }

    [TestMethod]
    public void Startup_fails_with_the_legacy_audit_key_or_an_invalid_administrator_entry()
    {
        AssertStartupFails("SecurityAudit__GlobalReviewerUserId", Guid.NewGuid().ToString());
        AssertStartupFails("Administration__GlobalAdministratorUserIds__0", "not-a-guid");
    }

    [TestMethod]
    public async Task Empty_administrator_list_gives_no_global_authority()
    {
        Environment.SetEnvironmentVariable("Administration__GlobalAdministratorUserIds__0", null);
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.ConfigureServices(_ => { }));
        using var scope = factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AuthenticationDbContext>().Database.MigrateAsync();
        Assert.IsFalse(scope.ServiceProvider.GetRequiredService<GaussAuth.Application.Administration.Ports.IGlobalAdministratorPolicy>().IsGlobalAdministrator(Guid.NewGuid()));
        using var ordinary = await factory.CreateApplicationAdministratorAsync();
        using var response = await ordinary.Client.GetAsync("/admin/applications");
        Assert.AreEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [TestMethod]
    public async Task Self_service_endpoints_ignore_externally_supplied_user_identifiers()
    {
        var policy = new TestGlobalAdministratorPolicy();
        using var factory = await MigratedFactoryAsync(policy);
        using var caller = await factory.CreateGlobalAdministratorAsync(policy);
        var otherUserId = Guid.NewGuid();

        using var validate = await caller.Client.PostAsJsonAsync($"/auth/session/validate?userId={otherUserId}",
            new { applicationCode = caller.ApplicationCode, userId = otherUserId });
        Assert.AreEqual(HttpStatusCode.OK, validate.StatusCode);
        Assert.AreEqual(caller.UserId, JsonDocument.Parse(await validate.Content.ReadAsStringAsync()).RootElement.GetProperty("userId").GetGuid());
        using var avatar = await caller.Client.DeleteAsync($"/me/profile/avatar?userId={otherUserId}");
        Assert.AreEqual(HttpStatusCode.NoContent, avatar.StatusCode);
    }

    [TestMethod]
    public async Task User_list_is_bounded_paginated_filtered_and_deterministic()
    {
        var policy = new TestGlobalAdministratorPolicy();
        using var factory = await MigratedFactoryAsync(policy);
        using var global = await factory.CreateGlobalAdministratorAsync(policy);
        var tag = Guid.NewGuid().ToString("N");
        var created = new List<Guid>();
        for (var i = 0; i < 3; i++) created.Add(await factory.CreateUserAsync($"list-{tag}-{i}@example.test"));
        using (var scope = factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<DeactivateUserHandler>().HandleAsync(created[0], CancellationToken.None);

        var seen = new List<Guid>();
        string? cursor = null;
        var pages = 0;
        do
        {
            using var page = await global.Client.GetAsync("/admin/users?limit=2" + (cursor is null ? "" : $"&cursor={cursor}"));
            Assert.AreEqual(HttpStatusCode.OK, page.StatusCode);
            var root = JsonDocument.Parse(await page.Content.ReadAsStringAsync()).RootElement;
            var items = root.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetGuid()).ToList();
            Assert.IsTrue(items.Count <= 2);
            seen.AddRange(items);
            cursor = root.GetProperty("nextCursor").GetString();
            pages++;
            Assert.IsTrue(pages < 10_000);
        }
        while (cursor is not null);
        Assert.IsGreaterThan(1, pages);
        CollectionAssert.AreEqual(seen.OrderBy(id => id).ToList(), seen);
        Assert.AreEqual(seen.Count, seen.Distinct().Count());
        foreach (var id in created) CollectionAssert.Contains(seen, id);

        using var first = await global.Client.GetAsync("/admin/users?limit=5");
        using var second = await global.Client.GetAsync("/admin/users?limit=5");
        Assert.AreEqual(await first.Content.ReadAsStringAsync(), await second.Content.ReadAsStringAsync());

        using var byEmail = await global.Client.GetAsync($"/admin/users?email=LIST-{tag}-1@EXAMPLE.test");
        var matched = JsonDocument.Parse(await byEmail.Content.ReadAsStringAsync()).RootElement.GetProperty("items");
        Assert.AreEqual(1, matched.GetArrayLength());
        Assert.AreEqual(created[1], matched[0].GetProperty("id").GetGuid());
        using var partial = await global.Client.GetAsync($"/admin/users?email=list-{tag}");
        Assert.AreEqual(0, JsonDocument.Parse(await partial.Content.ReadAsStringAsync()).RootElement.GetProperty("items").GetArrayLength());

        using var inactive = await global.Client.GetAsync("/admin/users?isActive=false&limit=100");
        var inactiveItems = JsonDocument.Parse(await inactive.Content.ReadAsStringAsync()).RootElement.GetProperty("items").EnumerateArray().ToList();
        Assert.IsTrue(inactiveItems.All(item => !item.GetProperty("isActive").GetBoolean()));
        Assert.IsTrue(inactiveItems.Any(item => item.GetProperty("id").GetGuid() == created[0]) || inactiveItems.Count == 100);

        foreach (var bad in new[] { "limit=101", "limit=0", "limit=-1", "cursor=not-a-guid", $"cursor={new string('a', 37)}", $"email={new string('a', 321)}", "email=%20" })
        {
            using var rejected = await global.Client.GetAsync("/admin/users?" + bad);
            Assert.AreEqual(HttpStatusCode.BadRequest, rejected.StatusCode, bad);
        }

        using var anonymous = factory.CreateClient();
        using var unauthenticated = await anonymous.GetAsync("/admin/users");
        Assert.AreEqual(HttpStatusCode.Unauthorized, unauthenticated.StatusCode);
    }

    [TestMethod]
    public async Task User_deactivation_blocks_sessions_and_login_but_preserves_memberships_and_is_idempotent()
    {
        var policy = new TestGlobalAdministratorPolicy();
        using var factory = await MigratedFactoryAsync(policy);
        using var global = await factory.CreateGlobalAdministratorAsync(policy);
        using var member = await factory.CreateApplicationAdministratorAsync();
        var userId = member.UserId;

        using var live = await member.Client.PostAsJsonAsync("/auth/session/validate", new { applicationCode = member.ApplicationCode });
        Assert.AreEqual(HttpStatusCode.OK, live.StatusCode);

        using var deactivated = await global.Client.PostAsync($"/admin/users/{userId}/deactivate", null);
        using var deactivatedAgain = await global.Client.PostAsync($"/admin/users/{userId}/deactivate", null);
        Assert.AreEqual(HttpStatusCode.OK, deactivated.StatusCode);
        Assert.AreEqual(HttpStatusCode.OK, deactivatedAgain.StatusCode);

        using var blocked = await member.Client.PostAsJsonAsync("/auth/session/validate", new { applicationCode = member.ApplicationCode });
        Assert.AreEqual(HttpStatusCode.Unauthorized, blocked.StatusCode);
        await Assert.ThrowsAsync<InvalidOperationException>(() => factory.SignInAsync(member.Email, member.ApplicationId, member.ApplicationCode));
        using var memberships = await global.Client.GetAsync($"/admin/users/{userId}/memberships");
        Assert.AreEqual(1, JsonDocument.Parse(await memberships.Content.ReadAsStringAsync()).RootElement.GetProperty("items").GetArrayLength());

        using var activated = await global.Client.PostAsync($"/admin/users/{userId}/activate", null);
        using var activatedAgain = await global.Client.PostAsync($"/admin/users/{userId}/activate", null);
        Assert.AreEqual(HttpStatusCode.OK, activated.StatusCode);
        Assert.AreEqual(HttpStatusCode.OK, activatedAgain.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuthenticationDbContext>();
        Assert.AreEqual(1, await db.SecurityEvents.CountAsync(item => item.EventType == "user.deactivated" && item.UserId == userId));
        Assert.AreEqual(1, await db.SecurityEvents.CountAsync(item => item.EventType == "user.activated" && item.UserId == userId));
        Assert.AreEqual(2, await db.SecurityEvents.CountAsync(item => item.ActorUserId == global.UserId && item.UserId == userId));

        // Application lifecycle follows the same one-event-per-real-change rule.
        var (applicationId, _) = await factory.CreateApplicationAsync();
        foreach (var path in new[] { "deactivate", "deactivate", "activate", "activate" })
        {
            using var response = await global.Client.PostAsync($"/admin/applications/{applicationId}/{path}", null);
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        }

        Assert.AreEqual(1, await db.SecurityEvents.CountAsync(item => item.EventType == "application.deactivated" && item.SubjectId == applicationId));
        Assert.AreEqual(1, await db.SecurityEvents.CountAsync(item => item.EventType == "application.activated" && item.SubjectId == applicationId));
    }

    [TestMethod]
    public async Task User_responses_hide_credential_material_and_management_routes_offer_no_delete()
    {
        var policy = new TestGlobalAdministratorPolicy();
        using var factory = await MigratedFactoryAsync(policy);
        using var global = await factory.CreateGlobalAdministratorAsync(policy);
        var userId = await factory.CreateUserAsync();

        using var created = await global.Client.PostAsJsonAsync("/admin/users", new
        {
            email = $"created-{Guid.NewGuid():N}@example.test", password = AdministrativeTestHost.Password, firstName = "A", lastName = "B", displayName = "AB",
        });
        Assert.AreEqual(HttpStatusCode.Created, created.StatusCode);
        using var detail = await global.Client.GetAsync($"/admin/users/{userId}");
        using var list = await global.Client.GetAsync("/admin/users?limit=100");
        foreach (var body in new[] { await created.Content.ReadAsStringAsync(), await detail.Content.ReadAsStringAsync(), await list.Content.ReadAsStringAsync() })
        {
            foreach (var forbidden in new[] { "passwordHash", "securityStamp", "concurrencyStamp", "resetToken", "recovery", "secret" })
                Assert.IsFalse(body.Contains(forbidden, StringComparison.OrdinalIgnoreCase), forbidden);
        }

        var (applicationId, _) = await factory.CreateApplicationAsync();
        foreach (var path in new[] { $"/admin/users/{userId}", $"/admin/applications/{applicationId}", "/admin/users", "/admin/applications" })
        {
            using var delete = await global.Client.DeleteAsync(path);
            Assert.IsTrue(delete.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed, path);
        }

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuthenticationDbContext>();
        var createdId = JsonDocument.Parse(await created.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetGuid();
        var recorded = await db.SecurityEvents.SingleAsync(item => item.EventType == "user.created" && item.SubjectId == createdId);
        Assert.AreEqual(global.UserId, recorded.ActorUserId);
        Assert.IsFalse((recorded.Metadata ?? string.Empty).Contains("example.test", StringComparison.OrdinalIgnoreCase));
    }

    private static readonly string[] AccessModelPermissions =
    [
        AdministrativePermissionCatalog.MembershipsRead, AdministrativePermissionCatalog.MembershipsManage,
        AdministrativePermissionCatalog.RolesRead, AdministrativePermissionCatalog.RolesManage,
        AdministrativePermissionCatalog.PermissionsRead, AdministrativePermissionCatalog.PermissionsManage,
    ];

    private static async Task<int> EventCountAsync(WebApplicationFactory<Program> factory, string eventType, Guid subjectId)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AuthenticationDbContext>().SecurityEvents
            .CountAsync(item => item.EventType == eventType && item.SubjectId == subjectId);
    }

    private static async Task<Guid> IdAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetGuid();

    [TestMethod]
    public async Task Application_administrator_manages_memberships_within_state_rules()
    {
        var policy = new TestGlobalAdministratorPolicy();
        using var factory = await MigratedFactoryAsync(policy);
        using var global = await factory.CreateGlobalAdministratorAsync(policy);
        var (applicationA, codeA) = await factory.CreateApplicationAsync();
        var (applicationB, _) = await factory.CreateApplicationAsync();
        using var adminA = await factory.CreateApplicationAdministratorAsync(applicationA, codeA, AccessModelPermissions);
        var userId = await factory.CreateUserAsync();

        using var created = await adminA.Client.PostAsJsonAsync($"/admin/applications/{applicationA}/memberships", new { userId });
        Assert.AreEqual(HttpStatusCode.Created, created.StatusCode);
        using var duplicate = await adminA.Client.PostAsJsonAsync($"/admin/applications/{applicationA}/memberships", new { userId });
        Assert.AreEqual(HttpStatusCode.Conflict, duplicate.StatusCode);
        var membershipId = await IdAsync(created);
        Assert.AreEqual(1, await EventCountAsync(factory, "membership.created", membershipId));

        using var deactivated = await adminA.Client.PostAsync($"/admin/applications/{applicationA}/memberships/{userId}/deactivate", null);
        using var activated = await adminA.Client.PostAsync($"/admin/applications/{applicationA}/memberships/{userId}/activate", null);
        using var activatedAgain = await adminA.Client.PostAsync($"/admin/applications/{applicationA}/memberships/{userId}/activate", null);
        Assert.AreEqual(HttpStatusCode.OK, deactivated.StatusCode);
        Assert.AreEqual(HttpStatusCode.OK, activated.StatusCode);
        Assert.AreEqual(HttpStatusCode.OK, activatedAgain.StatusCode);
        Assert.AreEqual(1, await EventCountAsync(factory, "membership.deactivated", membershipId));
        Assert.AreEqual(1, await EventCountAsync(factory, "membership.activated", membershipId));

        // An inactive user cannot have a membership activated.
        await adminA.Client.PostAsync($"/admin/applications/{applicationA}/memberships/{userId}/deactivate", null);
        using (var scope = factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<DeactivateUserHandler>().HandleAsync(userId, CancellationToken.None);
        using var inactiveUser = await adminA.Client.PostAsync($"/admin/applications/{applicationA}/memberships/{userId}/activate", null);
        Assert.AreEqual(HttpStatusCode.Conflict, inactiveUser.StatusCode);

        // An inactive Application accepts no new membership.
        await global.Client.PostAsync($"/admin/applications/{applicationB}/deactivate", null);
        var other = await factory.CreateUserAsync();
        using var inactiveApplication = await global.Client.PostAsJsonAsync($"/admin/applications/{applicationB}/memberships", new { userId = other });
        Assert.AreEqual(HttpStatusCode.Conflict, inactiveApplication.StatusCode);

        // Cross-application misuse is refused before anything is read.
        using var cross = await adminA.Client.PostAsJsonAsync($"/admin/applications/{applicationB}/memberships", new { userId = other });
        Assert.AreEqual(HttpStatusCode.Forbidden, cross.StatusCode);
        using var list = await adminA.Client.GetAsync($"/admin/applications/{applicationA}/memberships");
        Assert.AreEqual(HttpStatusCode.OK, list.StatusCode);
    }

    [TestMethod]
    public async Task Roles_and_permissions_are_unique_per_application_and_same_names_elsewhere_grant_nothing()
    {
        var policy = new TestGlobalAdministratorPolicy();
        using var factory = await MigratedFactoryAsync(policy);
        using var global = await factory.CreateGlobalAdministratorAsync(policy);
        var (applicationA, codeA) = await factory.CreateApplicationAsync();
        var (applicationB, codeB) = await factory.CreateApplicationAsync();
        using var adminA = await factory.CreateApplicationAdministratorAsync(applicationA, codeA, AccessModelPermissions);

        using var role = await adminA.Client.PostAsJsonAsync($"/admin/applications/{applicationA}/roles", new { name = "Reader", description = "d" });
        Assert.AreEqual(HttpStatusCode.Created, role.StatusCode);
        var roleId = await IdAsync(role);
        using var duplicateRole = await adminA.Client.PostAsJsonAsync($"/admin/applications/{applicationA}/roles", new { name = "reader" });
        Assert.AreEqual(HttpStatusCode.Conflict, duplicateRole.StatusCode);
        using var roleInB = await global.Client.PostAsJsonAsync($"/admin/applications/{applicationB}/roles", new { name = "Reader" });
        Assert.AreEqual(HttpStatusCode.Created, roleInB.StatusCode);

        using var permission = await adminA.Client.PostAsJsonAsync($"/admin/applications/{applicationA}/permissions", new { code = "reports.read" });
        Assert.AreEqual(HttpStatusCode.Created, permission.StatusCode);
        var permissionId = await IdAsync(permission);
        using var duplicatePermission = await adminA.Client.PostAsJsonAsync($"/admin/applications/{applicationA}/permissions", new { code = "reports.read" });
        Assert.AreEqual(HttpStatusCode.Conflict, duplicatePermission.StatusCode);
        using var permissionInB = await global.Client.PostAsJsonAsync($"/admin/applications/{applicationB}/permissions", new { code = "reports.read" });
        Assert.AreEqual(HttpStatusCode.Created, permissionInB.StatusCode);

        using var describeRole = await adminA.Client.PutAsJsonAsync($"/admin/applications/{applicationA}/roles/{roleId}/description", new { description = "changed" });
        using var describeRoleAgain = await adminA.Client.PutAsJsonAsync($"/admin/applications/{applicationA}/roles/{roleId}/description", new { description = "changed" });
        Assert.AreEqual(HttpStatusCode.OK, describeRole.StatusCode);
        Assert.AreEqual(HttpStatusCode.OK, describeRoleAgain.StatusCode);
        Assert.AreEqual(1, await EventCountAsync(factory, "role.updated", roleId));
        using var describePermission = await adminA.Client.PutAsJsonAsync($"/admin/applications/{applicationA}/permissions/{permissionId}/description", new { description = "changed" });
        Assert.AreEqual(HttpStatusCode.OK, describePermission.StatusCode);
        Assert.AreEqual(1, await EventCountAsync(factory, "permission.updated", permissionId));

        foreach (var path in new[] { "roles/" + roleId, "permissions/" + permissionId })
        {
            using var off = await adminA.Client.PostAsync($"/admin/applications/{applicationA}/{path}/deactivate", null);
            using var offAgain = await adminA.Client.PostAsync($"/admin/applications/{applicationA}/{path}/deactivate", null);
            using var on = await adminA.Client.PostAsync($"/admin/applications/{applicationA}/{path}/activate", null);
            using var onAgain = await adminA.Client.PostAsync($"/admin/applications/{applicationA}/{path}/activate", null);
            Assert.IsTrue(new[] { off, offAgain, on, onAgain }.All(response => response.StatusCode == HttpStatusCode.OK), path);
        }

        Assert.AreEqual(1, await EventCountAsync(factory, "role.deactivated", roleId));
        Assert.AreEqual(1, await EventCountAsync(factory, "role.activated", roleId));
        Assert.AreEqual(1, await EventCountAsync(factory, "permission.deactivated", permissionId));
        Assert.AreEqual(1, await EventCountAsync(factory, "permission.activated", permissionId));
        foreach (var path in new[] { "roles", "permissions" })
        {
            using var list = await adminA.Client.GetAsync($"/admin/applications/{applicationA}/{path}?limit=100");
            Assert.AreEqual(HttpStatusCode.OK, list.StatusCode);
        }

        // A user holding A's role and permission has nothing in B, which has same-named records.
        var userId = await factory.CreateUserAsync();
        await factory.CreateMembershipAsync(userId, applicationA);
        await factory.CreateMembershipAsync(userId, applicationB);
        using var rolePermission = await adminA.Client.PostAsync($"/admin/applications/{applicationA}/roles/{roleId}/permissions/{permissionId}", null);
        using var userRole = await adminA.Client.PostAsync($"/admin/applications/{applicationA}/users/{userId}/roles/{roleId}", null);
        Assert.AreEqual(HttpStatusCode.Created, rolePermission.StatusCode);
        Assert.AreEqual(HttpStatusCode.Created, userRole.StatusCode);
        using var viewA = await adminA.Client.GetAsync($"/admin/applications/{applicationA}/users/{userId}/authorization");
        using var viewB = await global.Client.GetAsync($"/admin/applications/{applicationB}/users/{userId}/authorization");
        CollectionAssert.AreEqual(new[] { "reports.read" }, PermissionCodes(await viewA.Content.ReadAsStringAsync()));
        Assert.AreEqual(0, PermissionCodes(await viewB.Content.ReadAsStringAsync()).Length);
        Assert.AreEqual(0, JsonDocument.Parse(await viewB.Content.ReadAsStringAsync()).RootElement.GetProperty("roles").GetArrayLength());
        _ = codeB;
    }

    [TestMethod]
    public async Task Assignments_require_membership_stay_inside_the_application_and_are_idempotent_with_correct_subjects()
    {
        var policy = new TestGlobalAdministratorPolicy();
        using var factory = await MigratedFactoryAsync(policy);
        using var global = await factory.CreateGlobalAdministratorAsync(policy);
        var (applicationA, codeA) = await factory.CreateApplicationAsync();
        var (applicationB, _) = await factory.CreateApplicationAsync();
        using var adminA = await factory.CreateApplicationAdministratorAsync(applicationA, codeA, AccessModelPermissions);
        var roleA = await IdAsync(await adminA.Client.PostAsJsonAsync($"/admin/applications/{applicationA}/roles", new { name = "A-role" }));
        var permissionA = await IdAsync(await adminA.Client.PostAsJsonAsync($"/admin/applications/{applicationA}/permissions", new { code = "a.thing" }));
        var permissionB = await IdAsync(await global.Client.PostAsJsonAsync($"/admin/applications/{applicationB}/permissions", new { code = "b.thing" }));
        var roleB = await IdAsync(await global.Client.PostAsJsonAsync($"/admin/applications/{applicationB}/roles", new { name = "B-role" }));
        var userId = await factory.CreateUserAsync();

        using var noMembership = await adminA.Client.PostAsync($"/admin/applications/{applicationA}/users/{userId}/roles/{roleA}", null);
        Assert.IsTrue(noMembership.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.NotFound);
        await factory.CreateMembershipAsync(userId, applicationA);

        using var crossPermission = await adminA.Client.PostAsync($"/admin/applications/{applicationA}/roles/{roleA}/permissions/{permissionB}", null);
        using var crossRole = await adminA.Client.PostAsync($"/admin/applications/{applicationA}/roles/{roleB}/permissions/{permissionA}", null);
        using var crossUserRole = await adminA.Client.PostAsync($"/admin/applications/{applicationA}/users/{userId}/roles/{roleB}", null);
        Assert.IsTrue(new[] { crossPermission, crossRole, crossUserRole }.All(response => response.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.NotFound));
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AuthenticationDbContext>();
            Assert.AreEqual(0, await db.RolePermissions.CountAsync(item => item.RoleId == roleA || item.RoleId == roleB));
            Assert.AreEqual(0, await db.UserRoles.CountAsync(item => item.UserId == userId));
        }

        using var first = await adminA.Client.PostAsync($"/admin/applications/{applicationA}/roles/{roleA}/permissions/{permissionA}", null);
        using var repeated = await adminA.Client.PostAsync($"/admin/applications/{applicationA}/roles/{roleA}/permissions/{permissionA}", null);
        Assert.AreEqual(HttpStatusCode.Created, first.StatusCode);
        Assert.AreEqual(HttpStatusCode.Conflict, repeated.StatusCode);
        var rolePermissionId = JsonDocument.Parse(await first.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetGuid();
        Assert.AreEqual(1, await EventCountAsync(factory, "permission.assigned", rolePermissionId));

        using var assigned = await adminA.Client.PostAsync($"/admin/applications/{applicationA}/users/{userId}/roles/{roleA}", null);
        using var assignedAgain = await adminA.Client.PostAsync($"/admin/applications/{applicationA}/users/{userId}/roles/{roleA}", null);
        Assert.AreEqual(HttpStatusCode.Created, assigned.StatusCode);
        Assert.AreEqual(HttpStatusCode.Conflict, assignedAgain.StatusCode);
        var userRoleId = await IdAsync(assigned);
        Assert.AreEqual(1, await EventCountAsync(factory, "role.assigned", userRoleId));
        using var removed = await adminA.Client.PostAsync($"/admin/applications/{applicationA}/users/{userId}/roles/{roleA}/remove", null);
        using var removedAgain = await adminA.Client.PostAsync($"/admin/applications/{applicationA}/users/{userId}/roles/{roleA}/remove", null);
        Assert.AreEqual(HttpStatusCode.OK, removed.StatusCode);
        Assert.AreEqual(HttpStatusCode.OK, removedAgain.StatusCode);
        Assert.AreEqual(1, await EventCountAsync(factory, "role.removed", userRoleId));

        using var scope2 = factory.Services.CreateScope();
        var events = scope2.ServiceProvider.GetRequiredService<AuthenticationDbContext>().SecurityEvents;
        Assert.AreEqual("role", await events.Where(item => item.EventType == "role.created" && item.SubjectId == roleA).Select(item => item.SubjectType).SingleAsync());
        Assert.AreEqual("permission", await events.Where(item => item.EventType == "permission.created" && item.SubjectId == permissionA).Select(item => item.SubjectType).SingleAsync());
        Assert.AreEqual("role-permission", await events.Where(item => item.SubjectId == rolePermissionId).Select(item => item.SubjectType).FirstAsync());
        Assert.AreEqual("user-role", await events.Where(item => item.SubjectId == userRoleId).Select(item => item.SubjectType).FirstAsync());
    }

    [TestMethod]
    public async Task Concurrent_identical_assignments_and_memberships_leave_exactly_one_record()
    {
        var policy = new TestGlobalAdministratorPolicy();
        using var factory = await MigratedFactoryAsync(policy);
        var (applicationId, _) = await factory.CreateApplicationAsync();
        var userId = await factory.CreateUserAsync();

        var memberships = await Task.WhenAll(Enumerable.Range(0, 4).Select(async _ =>
        {
            using var scope = factory.Services.CreateScope();
            return (await scope.ServiceProvider.GetRequiredService<GaussAuth.Application.Memberships.MembershipService>().CreateAsync(userId, applicationId, CancellationToken.None)).Membership is not null;
        }));
        Assert.AreEqual(1, memberships.Count(created => created));

        Guid roleId;
        using (var scope = factory.Services.CreateScope())
            roleId = (await scope.ServiceProvider.GetRequiredService<RoleService>().CreateAsync(applicationId, "Concurrent", null, CancellationToken.None)).Role!.Id;
        var assignments = await Task.WhenAll(Enumerable.Range(0, 4).Select(async _ =>
        {
            using var scope = factory.Services.CreateScope();
            return (await scope.ServiceProvider.GetRequiredService<UserRoleService>().AssignAsync(applicationId, userId, roleId, CancellationToken.None)).Created;
        }));
        Assert.AreEqual(1, assignments.Count(created => created));

        using var check = factory.Services.CreateScope();
        var db = check.ServiceProvider.GetRequiredService<AuthenticationDbContext>();
        Assert.AreEqual(1, await db.ApplicationMemberships.CountAsync(item => item.UserId == userId && item.ApplicationId == applicationId));
        Assert.AreEqual(1, await db.UserRoles.CountAsync(item => item.UserId == userId && item.RoleId == roleId));
    }

    [TestMethod]
    public async Task Administrator_removing_their_own_administrative_role_loses_access_and_global_administrator_restores_it()
    {
        var policy = new TestGlobalAdministratorPolicy();
        using var factory = await MigratedFactoryAsync(policy);
        using var global = await factory.CreateGlobalAdministratorAsync(policy);
        var (applicationId, code) = await factory.CreateApplicationAsync();
        var email = $"self-{Guid.NewGuid():N}@example.test";
        var userId = await factory.CreateUserAsync(email);
        await factory.CreateMembershipAsync(userId, applicationId);
        var roleId = await factory.GrantPermissionsAsync(applicationId, userId, AdministrativePermissionCatalog.RolesRead, AdministrativePermissionCatalog.RolesManage);
        using var admin = await factory.SignInAsync(email, applicationId, code);

        using var allowed = await admin.Client.GetAsync($"/admin/applications/{applicationId}/roles");
        Assert.AreEqual(HttpStatusCode.OK, allowed.StatusCode);
        using var removed = await admin.Client.PostAsync($"/admin/applications/{applicationId}/users/{userId}/roles/{roleId}/remove", null);
        Assert.AreEqual(HttpStatusCode.OK, removed.StatusCode);
        using var denied = await admin.Client.GetAsync($"/admin/applications/{applicationId}/roles");
        Assert.AreEqual(HttpStatusCode.Forbidden, denied.StatusCode);

        using var restored = await global.Client.PostAsync($"/admin/applications/{applicationId}/users/{userId}/roles/{roleId}", null);
        Assert.IsTrue(restored.StatusCode is HttpStatusCode.OK or HttpStatusCode.Created);
        using var again = await admin.Client.GetAsync($"/admin/applications/{applicationId}/roles");
        Assert.AreEqual(HttpStatusCode.OK, again.StatusCode);
    }

    [TestMethod]
    public async Task Authorization_view_matches_the_authorization_context_queries_and_omits_inactive_records()
    {
        var policy = new TestGlobalAdministratorPolicy();
        using var factory = await MigratedFactoryAsync(policy);
        using var global = await factory.CreateGlobalAdministratorAsync(policy);
        var (applicationId, _) = await factory.CreateApplicationAsync();
        var userId = await factory.CreateUserAsync();
        await factory.CreateMembershipAsync(userId, applicationId);
        var client = global.Client;

        var activeRole = await IdAsync(await client.PostAsJsonAsync($"/admin/applications/{applicationId}/roles", new { name = "Active" }));
        var inactiveRole = await IdAsync(await client.PostAsJsonAsync($"/admin/applications/{applicationId}/roles", new { name = "Inactive" }));
        var kept = await IdAsync(await client.PostAsJsonAsync($"/admin/applications/{applicationId}/permissions", new { code = "kept.read" }));
        var dropped = await IdAsync(await client.PostAsJsonAsync($"/admin/applications/{applicationId}/permissions", new { code = "dropped.read" }));
        var viaInactiveRole = await IdAsync(await client.PostAsJsonAsync($"/admin/applications/{applicationId}/permissions", new { code = "inactive-role.read" }));
        foreach (var (role, permission) in new[] { (activeRole, kept), (activeRole, dropped), (inactiveRole, viaInactiveRole) })
            await client.PostAsync($"/admin/applications/{applicationId}/roles/{role}/permissions/{permission}", null);
        foreach (var role in new[] { activeRole, inactiveRole })
            await client.PostAsync($"/admin/applications/{applicationId}/users/{userId}/roles/{role}", null);
        await client.PostAsync($"/admin/applications/{applicationId}/roles/{inactiveRole}/deactivate", null);
        await client.PostAsync($"/admin/applications/{applicationId}/permissions/{dropped}/deactivate", null);

        using var response = await client.GetAsync($"/admin/applications/{applicationId}/users/{userId}/authorization");
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var root = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.AreEqual(applicationId, root.GetProperty("application").GetProperty("id").GetGuid());
        Assert.IsTrue(root.GetProperty("user").GetProperty("isActive").GetBoolean());
        Assert.IsTrue(root.GetProperty("membership").GetProperty("isActive").GetBoolean());

        using var scope = factory.Services.CreateScope();
        var userRoles = scope.ServiceProvider.GetRequiredService<GaussAuth.Application.Authorization.Ports.IUserRoleRepository>();
        var expectedPermissions = (await userRoles.GetEffectivePermissionsAsync(userId, applicationId, CancellationToken.None))
            .Select(permission => permission.Code).Distinct().OrderBy(value => value, StringComparer.Ordinal).ToArray();
        var expectedRoles = (await userRoles.GetActiveRolesAsync(userId, applicationId, CancellationToken.None)).Select(role => role.Id).ToArray();
        CollectionAssert.AreEqual(expectedPermissions, PermissionCodes(await response.Content.ReadAsStringAsync()));
        CollectionAssert.AreEqual(new[] { "kept.read" }, expectedPermissions);
        CollectionAssert.AreEqual(expectedRoles, root.GetProperty("roles").EnumerateArray().Select(role => role.GetProperty("id").GetGuid()).ToArray());
        Assert.AreEqual(1, expectedRoles.Length);

        using var noMembership = await client.GetAsync($"/admin/applications/{applicationId}/users/{await factory.CreateUserAsync()}/authorization");
        Assert.AreEqual(JsonValueKind.Null, JsonDocument.Parse(await noMembership.Content.ReadAsStringAsync()).RootElement.GetProperty("membership").ValueKind);
        using var missing = await client.GetAsync($"/admin/applications/{applicationId}/users/{Guid.NewGuid()}/authorization");
        Assert.AreEqual(HttpStatusCode.NotFound, missing.StatusCode);
    }

    private static string[] PermissionCodes(string json) =>
        JsonDocument.Parse(json).RootElement.GetProperty("permissions").EnumerateArray().Select(item => item.GetString()!).ToArray();

    private static async Task<bool> SessionIsValidAsync(AdministratorClient session)
    {
        using var response = await session.Client.PostAsJsonAsync("/auth/session/validate", new { applicationCode = session.ApplicationCode });
        return response.StatusCode == HttpStatusCode.OK;
    }

    private static async Task<AdministratorClient> NewMemberSessionAsync(WebApplicationFactory<Program> factory, Guid applicationId, string code, Guid? existingUser = null, string? email = null)
    {
        email ??= $"session-{Guid.NewGuid():N}@example.test";
        if (existingUser is null)
        {
            var userId = await factory.CreateUserAsync(email);
            await factory.CreateMembershipAsync(userId, applicationId);
        }

        return await factory.SignInAsync(email, applicationId, code);
    }

    [TestMethod]
    public async Task Session_list_filters_paginates_and_is_scoped_to_the_application()
    {
        var policy = new TestGlobalAdministratorPolicy();
        var time = new MutableTimeProvider();
        using var factory = await MigratedFactoryAsync(policy, time);
        var (applicationA, codeA) = await factory.CreateApplicationAsync();
        var (applicationB, codeB) = await factory.CreateApplicationAsync();
        using var old1 = await NewMemberSessionAsync(factory, applicationA, codeA);
        using var old2 = await NewMemberSessionAsync(factory, applicationA, codeA);
        time.Advance(TimeSpan.FromHours(9));
        using var fresh = await NewMemberSessionAsync(factory, applicationA, codeA);
        using var inB = await NewMemberSessionAsync(factory, applicationB, codeB);
        using var adminA = await factory.CreateApplicationAdministratorAsync(applicationA, codeA,
            AdministrativePermissionCatalog.SessionsRead, AdministrativePermissionCatalog.SessionsRevoke);
        var userInA = old1.UserId;

        var seen = new List<Guid>();
        string? cursor = null;
        do
        {
            using var page = await adminA.Client.GetAsync($"/admin/applications/{applicationA}/sessions?limit=2" + (cursor is null ? "" : $"&cursor={cursor}"));
            Assert.AreEqual(HttpStatusCode.OK, page.StatusCode);
            var root = JsonDocument.Parse(await page.Content.ReadAsStringAsync()).RootElement;
            seen.AddRange(root.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetGuid()));
            cursor = root.GetProperty("nextCursor").GetString();
        }
        while (cursor is not null);
        Assert.AreEqual(seen.Count, seen.Distinct().Count());
        foreach (var session in new[] { old1, old2, fresh, adminA }) CollectionAssert.Contains(seen, session.SessionId);
        CollectionAssert.DoesNotContain(seen, inB.SessionId);

        async Task<List<Guid>> IdsAsync(string query)
        {
            using var response = await adminA.Client.GetAsync($"/admin/applications/{applicationA}/sessions?{query}");
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, query);
            return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetGuid()).ToList();
        }

        var expired = await IdsAsync("state=expired&limit=100");
        CollectionAssert.IsSubsetOf(new[] { old1.SessionId, old2.SessionId }, expired);
        CollectionAssert.DoesNotContain(expired, fresh.SessionId);
        var active = await IdsAsync("state=active&limit=100");
        CollectionAssert.Contains(active, fresh.SessionId);
        CollectionAssert.DoesNotContain(active, old1.SessionId);
        CollectionAssert.AreEqual(new[] { old1.SessionId }, await IdsAsync($"userId={userInA}&limit=100"));
        Assert.AreEqual(0, (await IdsAsync("state=revoked&limit=100")).Count(id => id == fresh.SessionId));

        using var revoke = await adminA.Client.PostAsync($"/admin/applications/{applicationA}/sessions/{fresh.SessionId}/revoke", null);
        Assert.AreEqual(HttpStatusCode.OK, revoke.StatusCode);
        CollectionAssert.Contains(await IdsAsync("state=revoked&limit=100"), fresh.SessionId);

        foreach (var bad in new[] { "limit=0", "limit=101", "state=bogus", "cursor=zzz" })
        {
            using var rejected = await adminA.Client.GetAsync($"/admin/applications/{applicationA}/sessions?{bad}");
            Assert.AreEqual(HttpStatusCode.BadRequest, rejected.StatusCode, bad);
        }

        using var otherApplication = await adminA.Client.GetAsync($"/admin/applications/{applicationB}/sessions");
        using var otherSession = await adminA.Client.GetAsync($"/admin/applications/{applicationB}/sessions/{inB.SessionId}");
        using var crossRevoke = await adminA.Client.PostAsync($"/admin/applications/{applicationB}/sessions/revoke", null);
        Assert.AreEqual(HttpStatusCode.Forbidden, otherApplication.StatusCode);
        Assert.AreEqual(HttpStatusCode.Forbidden, otherSession.StatusCode);
        Assert.AreEqual(HttpStatusCode.Forbidden, crossRevoke.StatusCode);
        using var wrongScope = await adminA.Client.GetAsync($"/admin/applications/{applicationA}/sessions/{inB.SessionId}");
        Assert.AreEqual(HttpStatusCode.NotFound, wrongScope.StatusCode);
        Assert.IsTrue(await SessionIsValidAsync(inB));

        // A caller holding only the read permission cannot revoke.
        using var reader = await factory.CreateApplicationAdministratorAsync(applicationA, codeA, AdministrativePermissionCatalog.SessionsRead);
        using var forbidden = await reader.Client.PostAsync($"/admin/applications/{applicationA}/sessions/{old1.SessionId}/revoke", null);
        Assert.AreEqual(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }

    [TestMethod]
    public async Task Revoking_sessions_by_scope_takes_effect_immediately_is_idempotent_and_audited_with_the_actor()
    {
        var policy = new TestGlobalAdministratorPolicy();
        using var factory = await MigratedFactoryAsync(policy);
        using var global = await factory.CreateGlobalAdministratorAsync(policy);
        var (applicationA, codeA) = await factory.CreateApplicationAsync();
        var (applicationB, codeB) = await factory.CreateApplicationAsync();
        var email = $"multi-{Guid.NewGuid():N}@example.test";
        var userId = await factory.CreateUserAsync(email);
        await factory.CreateMembershipAsync(userId, applicationA);
        await factory.CreateMembershipAsync(userId, applicationB);
        using var userInA = await factory.SignInAsync(email, applicationA, codeA);
        using var userInB = await factory.SignInAsync(email, applicationB, codeB);
        using var otherInA = await NewMemberSessionAsync(factory, applicationA, codeA);

        using var one = await global.Client.PostAsync($"/admin/applications/{applicationA}/sessions/{otherInA.SessionId}/revoke", null);
        var body = await one.Content.ReadAsStringAsync();
        Assert.AreEqual(HttpStatusCode.OK, one.StatusCode);
        Assert.IsFalse(await SessionIsValidAsync(otherInA));
        foreach (var forbidden in new[] { "token", "credential", "secret", "accessToken" })
            Assert.IsFalse(body.Contains(forbidden, StringComparison.OrdinalIgnoreCase), forbidden);
        using var oneAgain = await global.Client.PostAsync($"/admin/applications/{applicationA}/sessions/{otherInA.SessionId}/revoke", null);
        Assert.AreEqual(HttpStatusCode.OK, oneAgain.StatusCode);
        using var get = await global.Client.GetAsync($"/admin/applications/{applicationA}/sessions/{otherInA.SessionId}");
        Assert.AreEqual("revoked", JsonDocument.Parse(await get.Content.ReadAsStringAsync()).RootElement.GetProperty("state").GetString());

        using var inApplication = await global.Client.PostAsync($"/admin/applications/{applicationA}/users/{userId}/sessions/revoke", null);
        Assert.AreEqual(HttpStatusCode.OK, inApplication.StatusCode);
        var summary = JsonDocument.Parse(await inApplication.Content.ReadAsStringAsync()).RootElement;
        Assert.AreEqual(1, summary.GetProperty("revoked").GetInt32());
        Assert.IsFalse(summary.GetProperty("hasMore").GetBoolean());
        Assert.IsFalse(await SessionIsValidAsync(userInA));
        Assert.IsTrue(await SessionIsValidAsync(userInB));

        using var everywhere = await global.Client.PostAsync($"/admin/users/{userId}/sessions/revoke", null);
        Assert.AreEqual(1, JsonDocument.Parse(await everywhere.Content.ReadAsStringAsync()).RootElement.GetProperty("revoked").GetInt32());
        Assert.IsFalse(await SessionIsValidAsync(userInB));
        using var repeated = await global.Client.PostAsync($"/admin/users/{userId}/sessions/revoke", null);
        Assert.AreEqual(0, JsonDocument.Parse(await repeated.Content.ReadAsStringAsync()).RootElement.GetProperty("revoked").GetInt32());

        using var userSessions = await global.Client.GetAsync($"/admin/users/{userId}/sessions?state=revoked");
        Assert.AreEqual(2, JsonDocument.Parse(await userSessions.Content.ReadAsStringAsync()).RootElement.GetProperty("items").GetArrayLength());

        var (applicationC, codeC) = await factory.CreateApplicationAsync();
        using var c1 = await NewMemberSessionAsync(factory, applicationC, codeC);
        using var c2 = await NewMemberSessionAsync(factory, applicationC, codeC);
        using var allInC = await global.Client.PostAsync($"/admin/applications/{applicationC}/sessions/revoke", null);
        Assert.AreEqual(2, JsonDocument.Parse(await allInC.Content.ReadAsStringAsync()).RootElement.GetProperty("revoked").GetInt32());
        Assert.IsFalse(await SessionIsValidAsync(c1));
        Assert.IsFalse(await SessionIsValidAsync(c2));

        using var missingUser = await global.Client.PostAsync($"/admin/users/{Guid.NewGuid()}/sessions/revoke", null);
        Assert.AreEqual(HttpStatusCode.NotFound, missingUser.StatusCode);

        using var scope = factory.Services.CreateScope();
        var events = scope.ServiceProvider.GetRequiredService<AuthenticationDbContext>().SecurityEvents;
        foreach (var session in new[] { otherInA, userInA, userInB, c1, c2 })
        {
            var recorded = await events.Where(item => item.EventType == "session.revoked" && item.SubjectId == session.SessionId).ToListAsync();
            Assert.AreEqual(1, recorded.Count);
            Assert.AreEqual("session", recorded[0].SubjectType);
            Assert.AreEqual(global.UserId, recorded[0].ActorUserId);
        }
    }

    [TestMethod]
    public async Task Bulk_revocation_is_capped_and_reports_whether_more_remain()
    {
        Environment.SetEnvironmentVariable("Administration__MaxBulkSessionRevocation", "2");
        try
        {
            var policy = new TestGlobalAdministratorPolicy();
            using var factory = await MigratedFactoryAsync(policy);
            using var global = await factory.CreateGlobalAdministratorAsync(policy);
            var (applicationId, code) = await factory.CreateApplicationAsync();
            var email = $"cap-{Guid.NewGuid():N}@example.test";
            var userId = await factory.CreateUserAsync(email);
            await factory.CreateMembershipAsync(userId, applicationId);
            var sessions = new List<AdministratorClient>();
            for (var i = 0; i < 3; i++) sessions.Add(await factory.SignInAsync(email, applicationId, code));
            try
            {
                var results = new List<(int Revoked, bool HasMore)>();
                for (var i = 0; i < 3; i++)
                {
                    using var response = await global.Client.PostAsync($"/admin/users/{userId}/sessions/revoke", null);
                    var root = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
                    results.Add((root.GetProperty("revoked").GetInt32(), root.GetProperty("hasMore").GetBoolean()));
                }

                CollectionAssert.AreEqual(new[] { (2, true), (1, false), (0, false) }, results);
                foreach (var session in sessions) Assert.IsFalse(await SessionIsValidAsync(session));
            }
            finally { foreach (var session in sessions) session.Dispose(); }
        }
        finally { Environment.SetEnvironmentVariable("Administration__MaxBulkSessionRevocation", null); }
    }

    [TestMethod]
    public void Startup_fails_with_an_invalid_bulk_revocation_limit()
    {
        AssertStartupFails("Administration__MaxBulkSessionRevocation", "0");
        AssertStartupFails("Administration__MaxBulkSessionRevocation", "10001");
    }

    private static async Task<HttpStatusCode> ContextStatusAsync(WebApplicationFactory<Program> factory, AdministratorClient session, string applicationCode, string secret)
    {
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/auth/authorization-context");
        request.Headers.Authorization = new("Bearer", session.AccessToken);
        request.Headers.Add("X-GaussAuth-Application-Code", applicationCode);
        request.Headers.Add("X-GaussAuth-Consumer-Secret", secret);
        using var response = await client.SendAsync(request);
        return response.StatusCode;
    }

    private static async Task<string> SecretFromAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("secret").GetString()!;

    [TestMethod]
    public async Task Consumer_secret_lifecycle_shows_the_value_once_and_keeps_the_previous_until_retired()
    {
        var logs = new CapturingLoggerProvider();
        var policy = new TestGlobalAdministratorPolicy();
        using var factory = (await MigratedFactoryAsync(policy)).WithWebHostBuilder(builder => builder.ConfigureServices(services => services.AddLogging(logging => logging.AddProvider(logs))));
        using var global = await factory.CreateGlobalAdministratorAsync(policy);
        var (applicationA, codeA) = await factory.CreateApplicationAsync();
        var (applicationB, codeB) = await factory.CreateApplicationAsync();
        using var memberA = await NewMemberSessionAsync(factory, applicationA, codeA);
        using var memberB = await NewMemberSessionAsync(factory, applicationB, codeB);
        var route = $"/admin/applications/{applicationA}/consumer-secret";

        using var before = await global.Client.GetAsync(route);
        Assert.IsFalse(JsonDocument.Parse(await before.Content.ReadAsStringAsync()).RootElement.GetProperty("exists").GetBoolean());

        using var generated = await global.Client.PostAsync(route, null);
        Assert.AreEqual(HttpStatusCode.Created, generated.StatusCode);
        Assert.IsTrue(generated.Headers.CacheControl?.NoStore);
        var first = await SecretFromAsync(generated);
        Assert.IsGreaterThanOrEqualTo(40, first.Length);
        Assert.AreEqual(HttpStatusCode.OK, await ContextStatusAsync(factory, memberA, codeA, first));
        using var generatedAgain = await global.Client.PostAsync(route, null);
        Assert.AreEqual(HttpStatusCode.Conflict, generatedAgain.StatusCode);

        using var rotated = await global.Client.PostAsync(route + "/rotate", null);
        Assert.AreEqual(HttpStatusCode.OK, rotated.StatusCode);
        Assert.IsTrue(rotated.Headers.CacheControl?.NoStore);
        var second = await SecretFromAsync(rotated);
        Assert.AreNotEqual(first, second);
        Assert.AreEqual(HttpStatusCode.OK, await ContextStatusAsync(factory, memberA, codeA, first));
        Assert.AreEqual(HttpStatusCode.OK, await ContextStatusAsync(factory, memberA, codeA, second));

        // The secret of Application A neither authenticates for B nor changes B's state.
        Assert.AreEqual(HttpStatusCode.Unauthorized, await ContextStatusAsync(factory, memberB, codeB, second));
        using var metadataB = await global.Client.GetAsync($"/admin/applications/{applicationB}/consumer-secret");
        Assert.IsFalse(JsonDocument.Parse(await metadataB.Content.ReadAsStringAsync()).RootElement.GetProperty("exists").GetBoolean());

        using var retired = await global.Client.PostAsync(route + "/retire-previous", null);
        Assert.AreEqual(HttpStatusCode.OK, retired.StatusCode);
        Assert.AreEqual(HttpStatusCode.Unauthorized, await ContextStatusAsync(factory, memberA, codeA, first));
        Assert.AreEqual(HttpStatusCode.OK, await ContextStatusAsync(factory, memberA, codeA, second));
        using var retiredAgain = await global.Client.PostAsync(route + "/retire-previous", null);
        Assert.AreEqual(HttpStatusCode.OK, retiredAgain.StatusCode);

        using var metadata = await global.Client.GetAsync(route);
        var metadataBody = await metadata.Content.ReadAsStringAsync();
        var root = JsonDocument.Parse(metadataBody).RootElement;
        CollectionAssert.AreEquivalent(new[] { "exists", "source", "createdAtUtc", "rotatedAtUtc", "hasRetiring" }, root.EnumerateObject().Select(item => item.Name).ToArray());
        Assert.AreEqual("managed", root.GetProperty("source").GetString());
        Assert.IsFalse(root.GetProperty("hasRetiring").GetBoolean());
        Assert.IsTrue(metadata.Headers.CacheControl?.NoStore);
        Assert.AreEqual(HttpStatusCode.OK, retired.StatusCode);
        var retiredBody = await retired.Content.ReadAsStringAsync();

        // Authorization: only global administrators, and a missing Application is a 404.
        using var applicationAdmin = await factory.CreateApplicationAdministratorAsync(applicationA, codeA, AccessModelPermissions);
        using var forbidden = await applicationAdmin.Client.PostAsync(route + "/rotate", null);
        using var forbiddenRead = await applicationAdmin.Client.GetAsync(route);
        using var anonymous = factory.CreateClient();
        using var unauthenticated = await anonymous.PostAsync(route, null);
        using var missing = await global.Client.PostAsync($"/admin/applications/{Guid.NewGuid()}/consumer-secret", null);
        Assert.AreEqual(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.AreEqual(HttpStatusCode.Forbidden, forbiddenRead.StatusCode);
        Assert.AreEqual(HttpStatusCode.Unauthorized, unauthenticated.StatusCode);
        Assert.AreEqual(HttpStatusCode.NotFound, missing.StatusCode);

        // Audit events carry actor and target but no secret material; nothing else ever shows the plaintext.
        using var scope = factory.Services.CreateScope();
        var events = await scope.ServiceProvider.GetRequiredService<AuthenticationDbContext>().SecurityEvents
            .Where(item => item.SubjectId == applicationA && item.SubjectType == "consumer-credential").ToListAsync();
        CollectionAssert.AreEquivalent(
            new[] { "consumer.credential.generated", "consumer.credential.rotated", "consumer.credential.previous-retired", "consumer.credential.previous-retired" },
            events.Select(item => item.EventType).ToArray());
        Assert.IsTrue(events.All(item => item.ActorUserId == global.UserId && item.ApplicationId == applicationA));
        var everything = JsonSerializer.Serialize(events) + metadataBody + retiredBody + string.Join('\n', logs.Entries);
        foreach (var secret in new[] { first, second })
        {
            Assert.IsFalse(everything.Contains(secret, StringComparison.Ordinal));
            Assert.IsFalse(everything.Contains(secret[..8], StringComparison.Ordinal));
        }

        Assert.IsFalse(new GaussAuth.Application.Administration.ConsumerCredentials.ConsumerSecretIssuance(first,
            new(true, "managed", null, null, false)).ToString().Contains(first, StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task Configured_hashes_keep_working_until_the_first_rotation_and_are_then_imported_as_retiring()
    {
        var code = $"cfg-{Guid.NewGuid():N}";
        var configuredSecret = $"configured-{Guid.NewGuid():N}";
        var variable = $"AuthorizationConsumers__{code}__CurrentSecretHash";
        Environment.SetEnvironmentVariable(variable, new PasswordHasher<string>().HashPassword(code, configuredSecret));
        try
        {
            var policy = new TestGlobalAdministratorPolicy();
            using var factory = await MigratedFactoryAsync(policy);
            using var global = await factory.CreateGlobalAdministratorAsync(policy);
            Guid applicationId;
            using (var scope = factory.Services.CreateScope())
                applicationId = (await scope.ServiceProvider.GetRequiredService<ApplicationService>().CreateAsync(code, "Configured", CancellationToken.None)).Application!.Id;
            using var member = await NewMemberSessionAsync(factory, applicationId, code);
            var route = $"/admin/applications/{applicationId}/consumer-secret";

            using var metadata = await global.Client.GetAsync(route);
            var before = JsonDocument.Parse(await metadata.Content.ReadAsStringAsync()).RootElement;
            Assert.IsTrue(before.GetProperty("exists").GetBoolean());
            Assert.AreEqual("configured", before.GetProperty("source").GetString());
            Assert.AreEqual(HttpStatusCode.OK, await ContextStatusAsync(factory, member, code, configuredSecret));
            using var conflict = await global.Client.PostAsync(route, null);
            Assert.AreEqual(HttpStatusCode.Conflict, conflict.StatusCode);
            using var noManaged = await global.Client.PostAsync(route + "/retire-previous", null);
            Assert.AreEqual(HttpStatusCode.Conflict, noManaged.StatusCode);

            using var rotated = await global.Client.PostAsync(route + "/rotate", null);
            Assert.AreEqual(HttpStatusCode.OK, rotated.StatusCode);
            var fresh = await SecretFromAsync(rotated);
            Assert.AreEqual(HttpStatusCode.OK, await ContextStatusAsync(factory, member, code, configuredSecret));
            Assert.AreEqual(HttpStatusCode.OK, await ContextStatusAsync(factory, member, code, fresh));

            using var retired = await global.Client.PostAsync(route + "/retire-previous", null);
            Assert.AreEqual(HttpStatusCode.OK, retired.StatusCode);
            Assert.AreEqual(HttpStatusCode.Unauthorized, await ContextStatusAsync(factory, member, code, configuredSecret));
            Assert.AreEqual(HttpStatusCode.OK, await ContextStatusAsync(factory, member, code, fresh));
        }
        finally { Environment.SetEnvironmentVariable(variable, null); }
    }

    [TestMethod]
    public async Task Concurrent_rotations_leave_one_consistent_active_and_retiring_state()
    {
        var policy = new TestGlobalAdministratorPolicy();
        using var factory = await MigratedFactoryAsync(policy);
        using var global = await factory.CreateGlobalAdministratorAsync(policy);
        var (applicationId, code) = await factory.CreateApplicationAsync();
        using var member = await NewMemberSessionAsync(factory, applicationId, code);
        var route = $"/admin/applications/{applicationId}/consumer-secret";
        using var generated = await global.Client.PostAsync(route, null);
        var original = await SecretFromAsync(generated);

        var responses = await Task.WhenAll(Enumerable.Range(0, 4).Select(async _ =>
        {
            using var response = await global.Client.PostAsync(route + "/rotate", null);
            return (response.StatusCode, Secret: response.StatusCode == HttpStatusCode.OK ? await SecretFromAsync(response) : null);
        }));
        Assert.IsTrue(responses.All(item => item.StatusCode is HttpStatusCode.OK or HttpStatusCode.Conflict));
        var winners = responses.Where(item => item.Secret is not null).Select(item => item.Secret!).ToArray();
        Assert.IsGreaterThanOrEqualTo(1, winners.Length);

        var working = 0;
        foreach (var secret in winners.Append(original))
            if (await ContextStatusAsync(factory, member, code, secret) == HttpStatusCode.OK) working++;
        Assert.IsTrue(working is >= 1 and <= 2, $"{working} secrets work");
        using var metadata = await global.Client.GetAsync(route);
        Assert.IsTrue(JsonDocument.Parse(await metadata.Content.ReadAsStringAsync()).RootElement.GetProperty("hasRetiring").GetBoolean());
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuthenticationDbContext>();
        Assert.AreEqual(1, await db.ConsumerCredentials.CountAsync(item => item.ApplicationId == applicationId));
        Assert.AreEqual(winners.Length, await db.SecurityEvents.CountAsync(item => item.EventType == "consumer.credential.rotated" && item.SubjectId == applicationId));
    }

    [TestMethod]
    public async Task Failed_audit_write_rolls_back_the_rotation_and_returns_no_plaintext()
    {
        var policy = new TestGlobalAdministratorPolicy();
        using var factory = (await MigratedFactoryAsync(policy)).WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<ISecurityEventRecorder>();
            services.AddScoped<ISecurityEventRecorder>(_ => new FailingEventRecorder(SecurityEventType.ConsumerCredentialRotated));
        }));
        using var global = await factory.CreateGlobalAdministratorAsync(policy);
        var (applicationId, code) = await factory.CreateApplicationAsync();
        using var member = await NewMemberSessionAsync(factory, applicationId, code);
        var route = $"/admin/applications/{applicationId}/consumer-secret";
        using var generated = await global.Client.PostAsync(route, null);
        Assert.AreEqual(HttpStatusCode.Created, generated.StatusCode);
        var original = await SecretFromAsync(generated);

        using var failed = await global.Client.PostAsync(route + "/rotate", null);
        var body = await failed.Content.ReadAsStringAsync();
        Assert.AreEqual(HttpStatusCode.InternalServerError, failed.StatusCode);
        Assert.IsFalse(body.Contains("secret", StringComparison.OrdinalIgnoreCase));
        Assert.AreEqual(HttpStatusCode.OK, await ContextStatusAsync(factory, member, code, original));
        using var metadata = await global.Client.GetAsync(route);
        var root = JsonDocument.Parse(await metadata.Content.ReadAsStringAsync()).RootElement;
        Assert.IsFalse(root.GetProperty("hasRetiring").GetBoolean());
        Assert.AreEqual(JsonValueKind.Null, root.GetProperty("rotatedAtUtc").ValueKind);
    }

    [TestMethod]
    public async Task Every_administrative_change_records_one_event_with_actor_and_target()
    {
        var policy = new TestGlobalAdministratorPolicy();
        using var factory = await MigratedFactoryAsync(policy);
        using var global = await factory.CreateGlobalAdministratorAsync(policy);
        var client = global.Client;
        var (applicationId, code) = await factory.CreateApplicationAsync();
        var targetUser = await factory.CreateUserAsync();
        await factory.CreateMembershipAsync(targetUser, applicationId);
        using var targetSession = await factory.SignInAsync((await WithDbAsync(factory, db => db.DomainUsers.Where(user => user.Id == targetUser).Select(user => user.Email).SingleAsync())), applicationId, code);

        await client.PostAsync($"/admin/users/{targetUser}/deactivate", null);
        await client.PostAsync($"/admin/users/{targetUser}/activate", null);
        await client.PostAsync($"/admin/applications/{applicationId}/deactivate", null);
        await client.PostAsync($"/admin/applications/{applicationId}/activate", null);
        await client.PostAsync($"/admin/applications/{applicationId}/memberships/{targetUser}/deactivate", null);
        var roleId = await IdAsync(await client.PostAsJsonAsync($"/admin/applications/{applicationId}/roles", new { name = "Audited" }));
        var permissionId = await IdAsync(await client.PostAsJsonAsync($"/admin/applications/{applicationId}/permissions", new { code = "audited.read" }));
        await client.PostAsync($"/admin/applications/{applicationId}/memberships/{targetUser}/activate", null);
        var rolePermissionId = await IdAsync(await client.PostAsync($"/admin/applications/{applicationId}/roles/{roleId}/permissions/{permissionId}", null));
        var userRoleId = await IdAsync(await client.PostAsync($"/admin/applications/{applicationId}/users/{targetUser}/roles/{roleId}", null));
        await client.PostAsync($"/admin/applications/{applicationId}/sessions/{targetSession.SessionId}/revoke", null);
        await client.PostAsync($"/admin/applications/{applicationId}/consumer-secret", null);
        await client.PostAsync($"/admin/applications/{applicationId}/consumer-secret/rotate", null);
        await client.PostAsync($"/admin/applications/{applicationId}/consumer-secret/retire-previous", null);

        var events = await WithDbAsync(factory, db => db.SecurityEvents.Where(item => item.ActorUserId == global.UserId).ToListAsync());
        void Expect(string type, Guid? userId, Guid? subjectId, string? subjectType)
        {
            var matches = events.Where(item => item.EventType == type && (userId is null || item.UserId == userId) && item.SubjectId == subjectId).ToList();
            Assert.AreEqual(1, matches.Count, $"{type} {subjectId}");
            Assert.AreEqual(subjectType, matches[0].SubjectType, type);
        }

        Expect("user.deactivated", targetUser, null, null);
        Expect("user.activated", targetUser, null, null);
        Expect("application.deactivated", null, applicationId, "application");
        Expect("application.activated", null, applicationId, "application");
        Expect("role.created", null, roleId, "role");
        Expect("permission.created", null, permissionId, "permission");
        Expect("permission.assigned", null, rolePermissionId, "role-permission");
        Expect("role.assigned", targetUser, userRoleId, "user-role");
        Expect("session.revoked", targetUser, targetSession.SessionId, "session");
        Expect("consumer.credential.generated", null, applicationId, "consumer-credential");
        Expect("consumer.credential.rotated", null, applicationId, "consumer-credential");
        Expect("consumer.credential.previous-retired", null, applicationId, "consumer-credential");
        Assert.AreEqual(1, events.Count(item => item.EventType == "membership.deactivated" && item.UserId == targetUser && item.SubjectType == "membership"));
        Assert.AreEqual(1, events.Count(item => item.EventType == "membership.activated" && item.UserId == targetUser && item.SubjectType == "membership"));
        Assert.IsTrue(events.Where(item => item.EventType != "user.deactivated" && item.EventType != "user.activated" && item.EventType.StartsWith("application.", StringComparison.Ordinal) == false)
            .All(item => item.ApplicationId == applicationId || item.ApplicationId is null));
    }

    [TestMethod]
    public async Task Actor_comes_from_the_session_even_when_the_target_is_the_same_user()
    {
        var policy = new TestGlobalAdministratorPolicy();
        using var factory = await MigratedFactoryAsync(policy);
        var (applicationId, code) = await factory.CreateApplicationAsync();
        var email = $"self-{Guid.NewGuid():N}@example.test";
        var userId = await factory.CreateUserAsync(email);
        await factory.CreateMembershipAsync(userId, applicationId);
        await factory.GrantPermissionsAsync(applicationId, userId, AdministrativePermissionCatalog.RolesManage, AdministrativePermissionCatalog.RolesRead);
        using var admin = await factory.SignInAsync(email, applicationId, code);
        var roleId = await IdAsync(await admin.Client.PostAsJsonAsync($"/admin/applications/{applicationId}/roles", new { name = "Self" }));

        using var assigned = await admin.Client.PostAsync($"/admin/applications/{applicationId}/users/{userId}/roles/{roleId}", null);
        Assert.AreEqual(HttpStatusCode.Created, assigned.StatusCode);
        var userRoleId = await IdAsync(assigned);
        var recorded = await WithDbAsync(factory, db => db.SecurityEvents.SingleAsync(item => item.EventType == "role.assigned" && item.SubjectId == userRoleId));
        Assert.AreEqual(userId, recorded.UserId);
        Assert.AreEqual(userId, recorded.ActorUserId);
    }

    [TestMethod]
    public async Task Denied_authenticated_attempts_are_audited_and_unauthenticated_ones_are_not()
    {
        var policy = new TestGlobalAdministratorPolicy();
        using var factory = await MigratedFactoryAsync(policy);
        var (applicationId, code) = await factory.CreateApplicationAsync();
        using var ordinary = await factory.CreateApplicationAdministratorAsync(applicationId, code);
        Task<int> DeniedAsync() => WithDbAsync(factory, db => db.SecurityEvents.CountAsync(item => item.EventType == "administration.access.denied"));

        var before = await DeniedAsync();
        using var anonymous = factory.CreateClient();
        using var unauthenticated = await anonymous.GetAsync("/admin/applications");
        Assert.AreEqual(HttpStatusCode.Unauthorized, unauthenticated.StatusCode);
        Assert.AreEqual(before, await DeniedAsync());

        using var denied = await ordinary.Client.GetAsync("/admin/applications");
        Assert.AreEqual(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.AreEqual(before + 1, await DeniedAsync());
        var recorded = await WithDbAsync(factory, db => db.SecurityEvents.Where(item => item.EventType == "administration.access.denied" && item.ActorUserId == ordinary.UserId).SingleAsync());
        Assert.AreEqual("rejected", recorded.Outcome);
    }

    [TestMethod]
    public async Task Application_reviewer_sees_only_its_application_and_the_global_administrator_sees_all_without_secrets()
    {
        var policy = new TestGlobalAdministratorPolicy();
        using var factory = await MigratedFactoryAsync(policy);
        using var global = await factory.CreateGlobalAdministratorAsync(policy);
        var (applicationA, codeA) = await factory.CreateApplicationAsync();
        var (applicationB, _) = await factory.CreateApplicationAsync();
        using var reviewer = await factory.CreateApplicationAdministratorAsync(applicationA, codeA, AdministrativePermissionCatalog.SecurityAuditRead);
        using var noAudit = await factory.CreateApplicationAdministratorAsync(applicationA, codeA, AdministrativePermissionCatalog.RolesRead);

        await global.Client.PostAsJsonAsync($"/admin/applications/{applicationA}/roles", new { name = "In-A" });
        await global.Client.PostAsJsonAsync($"/admin/applications/{applicationB}/roles", new { name = "In-B" });
        using var generated = await global.Client.PostAsync($"/admin/applications/{applicationA}/consumer-secret", null);
        var secret = await SecretFromAsync(generated);

        using var own = await reviewer.Client.GetAsync("/security-events?pageSize=100");
        Assert.AreEqual(HttpStatusCode.OK, own.StatusCode);
        var ownBody = await own.Content.ReadAsStringAsync();
        var ownItems = JsonDocument.Parse(ownBody).RootElement.GetProperty("items").EnumerateArray().ToList();
        Assert.IsGreaterThan(0, ownItems.Count);
        Assert.IsTrue(ownItems.All(item => item.GetProperty("applicationId").GetGuid() == applicationA));
        Assert.IsTrue(ownItems.Any(item => item.GetProperty("actorUserId").ValueKind == JsonValueKind.String && item.GetProperty("actorUserId").GetGuid() == global.UserId));
        using var otherApplication = await reviewer.Client.GetAsync($"/security-events?applicationId={applicationB}");
        Assert.AreEqual(HttpStatusCode.Forbidden, otherApplication.StatusCode);
        using var withoutPermission = await noAudit.Client.GetAsync("/security-events");
        Assert.AreEqual(HttpStatusCode.Forbidden, withoutPermission.StatusCode);

        using var all = await global.Client.GetAsync("/security-events?pageSize=100&eventType=role.created");
        var allBody = await all.Content.ReadAsStringAsync();
        var applications = JsonDocument.Parse(allBody).RootElement.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("applicationId").GetGuid()).ToList();
        CollectionAssert.IsSubsetOf(new[] { applicationA, applicationB }, applications);
        using var everything = await global.Client.GetAsync("/security-events?pageSize=100");
        Assert.IsFalse((ownBody + await everything.Content.ReadAsStringAsync()).Contains(secret, StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task Profile_updates_are_audited_only_on_real_change_without_personal_data_and_with_the_correlation_id()
    {
        var policy = new TestGlobalAdministratorPolicy();
        using var factory = await MigratedFactoryAsync(policy);
        using var global = await factory.CreateGlobalAdministratorAsync(policy);
        var userId = await factory.CreateUserAsync();
        var body = new { firstName = "Grace", lastName = "Hopper", displayName = "Grace H", phoneNumber = "+15551230000" };

        using var first = await global.Client.PutAsJsonAsync($"/admin/users/{userId}/profile", body);
        using var repeated = await global.Client.PutAsJsonAsync($"/admin/users/{userId}/profile", body);
        Assert.AreEqual(HttpStatusCode.OK, first.StatusCode);
        Assert.AreEqual(HttpStatusCode.OK, repeated.StatusCode);
        var recorded = await WithDbAsync(factory, db => db.SecurityEvents.Where(item => item.EventType == "user.profile.updated" && item.UserId == userId).ToListAsync());
        Assert.AreEqual(1, recorded.Count);
        Assert.AreEqual(global.UserId, recorded[0].ActorUserId);
        Assert.AreEqual(first.Headers.GetValues("X-Correlation-Id").Single(), recorded[0].CorrelationId);
        Assert.IsFalse(JsonSerializer.Serialize(recorded).Contains("Grace", StringComparison.Ordinal));
        Assert.IsFalse(JsonSerializer.Serialize(recorded).Contains("5551230000", StringComparison.Ordinal));

        // Every administrative event carries the correlation id of its request.
        var (applicationId, _) = await factory.CreateApplicationAsync();
        using var role = await global.Client.PostAsJsonAsync($"/admin/applications/{applicationId}/roles", new { name = "Correlated" });
        var roleId = await IdAsync(role);
        var roleEvent = await WithDbAsync(factory, db => db.SecurityEvents.SingleAsync(item => item.EventType == "role.created" && item.SubjectId == roleId));
        Assert.AreEqual(role.Headers.GetValues("X-Correlation-Id").Single(), roleEvent.CorrelationId);
    }

    [TestMethod]
    public async Task Oversized_fields_are_rejected_on_administrative_routes()
    {
        var policy = new TestGlobalAdministratorPolicy();
        using var factory = await MigratedFactoryAsync(policy);
        using var global = await factory.CreateGlobalAdministratorAsync(policy);
        var (applicationId, _) = await factory.CreateApplicationAsync();
        var applications = $"/admin/applications/{applicationId}";
        var cases = new (string Path, object Body)[]
        {
            ("/admin/applications", new { code = new string('a', 65), name = "N" }),
            ("/admin/applications", new { code = $"app-{Guid.NewGuid():N}", name = new string('n', 201) }),
            (applications + "/roles", new { name = new string('r', 201) }),
            (applications + "/roles", new { name = "R", description = new string('d', 501) }),
            (applications + "/permissions", new { code = new string('p', 129) }),
            (applications + "/permissions", new { code = "ok.code", description = new string('d', 501) }),
        };
        foreach (var (path, body) in cases)
        {
            using var response = await global.Client.PostAsJsonAsync(path, body);
            Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode, path);
        }
    }

    private static async Task<T> WithDbAsync<T>(WebApplicationFactory<Program> factory, Func<AuthenticationDbContext, Task<T>> query)
    {
        using var scope = factory.Services.CreateScope();
        return await query(scope.ServiceProvider.GetRequiredService<AuthenticationDbContext>());
    }

    private static async Task<WebApplicationFactory<Program>> MigratedFactoryAsync(TestGlobalAdministratorPolicy policy, TimeProvider? time = null)
    {
        var factory = new WebApplicationFactory<Program>().WithGlobalAdministrators(policy);
        if (time is not null)
            factory = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton(time);
            }));
        using var scope = factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AuthenticationDbContext>().Database.MigrateAsync();
        return factory;
    }

    private static async Task<string> ProblemShapeAsync(HttpResponseMessage response)
    {
        var root = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        return $"{(int)response.StatusCode}|{root.GetProperty("title").GetString()}|{root.GetProperty("status").GetInt32()}";
    }

    private static void AssertStartupFails(string environmentKey, string value)
    {
        Environment.SetEnvironmentVariable(environmentKey, value);
        try
        {
            using var factory = new WebApplicationFactory<Program>();
            Assert.ThrowsExactly<InvalidOperationException>(() => factory.CreateClient().Dispose());
        }
        finally { Environment.SetEnvironmentVariable(environmentKey, null); }
    }

    private static IConfiguration Configuration(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();
}
