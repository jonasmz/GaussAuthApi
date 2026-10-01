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
using GaussAuth.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

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
