using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GaussAuth.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GaussAuth.Foundation.Tests;

[TestClass]
public sealed class EffectivePermissionsTests
{
    // T034: active full path, duplicate elimination, canonical ordering, empty result.
    [TestMethod]
    public async Task Effective_permissions_are_unique_and_canonically_ordered_for_active_path()
    {
        using var factory = await FactoryAsync(); using var client = factory.CreateClient();
        var userId = await CreateUserAsync(client);
        var applicationId = await CreateApplicationAsync(client);
        await CreateMembershipAsync(client, userId, applicationId);

        var firstRoleId = await CreateRoleAsync(client, applicationId, "Role-One");
        var secondRoleId = await CreateRoleAsync(client, applicationId, "Role-Two");
        var sharedPermissionId = await CreatePermissionAsync(client, applicationId, "shared.permission");
        var zPermissionId = await CreatePermissionAsync(client, applicationId, "z.permission");
        var aPermissionId = await CreatePermissionAsync(client, applicationId, "a.permission");

        await AssignRolePermissionAsync(client, applicationId, firstRoleId, sharedPermissionId);
        await AssignRolePermissionAsync(client, applicationId, secondRoleId, sharedPermissionId);
        await AssignRolePermissionAsync(client, applicationId, firstRoleId, zPermissionId);
        await AssignRolePermissionAsync(client, applicationId, secondRoleId, aPermissionId);
        await AssignUserRoleAsync(client, applicationId, userId, firstRoleId);
        await AssignUserRoleAsync(client, applicationId, userId, secondRoleId);

        using var response = await client.GetAsync($"/applications/{applicationId}/users/{userId}/effective-permissions");
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var items = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("items");
        var codes = items.EnumerateArray().Select(x => x.GetProperty("code").GetString()).ToArray();
        CollectionAssert.AreEqual(new[] { "a.permission", "shared.permission", "z.permission" }, codes);

        var userWithoutAssignments = await CreateUserAsync(client);
        await CreateMembershipAsync(client, userWithoutAssignments, applicationId);
        using var empty = await client.GetAsync($"/applications/{applicationId}/users/{userWithoutAssignments}/effective-permissions");
        Assert.AreEqual(HttpStatusCode.OK, empty.StatusCode);
        Assert.AreEqual(0, JsonDocument.Parse(await empty.Content.ReadAsStringAsync()).RootElement.GetProperty("items").GetArrayLength());

        using var missingUser = await client.GetAsync($"/applications/{applicationId}/users/{Guid.NewGuid()}/effective-permissions");
        Assert.AreEqual(HttpStatusCode.NotFound, missingUser.StatusCode);
        using var missingApplication = await client.GetAsync($"/applications/{Guid.NewGuid()}/users/{userId}/effective-permissions");
        Assert.AreEqual(HttpStatusCode.NotFound, missingApplication.StatusCode);
    }

    // T035: each inactive link in the path independently excludes the permission while history is preserved.
    [TestMethod]
    public async Task Effective_permissions_exclude_results_when_any_link_in_the_path_is_inactive()
    {
        using var factory = await FactoryAsync(); using var client = factory.CreateClient();

        // A single user/application pair is reused across the independently-toggled scenarios below
        // (each with its own role/permission pair) to stay under the user-creation rate limit.
        var applicationId = await CreateApplicationAsync(client);
        var userId = await CreateUserAsync(client);
        await CreateMembershipAsync(client, userId, applicationId);

        async Task<(Guid RoleId, Guid PermissionId)> GrantRoleAsync(HttpClient c, string suffix)
        {
            var roleId = await CreateRoleAsync(c, applicationId, $"Role-{suffix}");
            var permissionId = await CreatePermissionAsync(c, applicationId, $"scoped.permission.{suffix}");
            await AssignRolePermissionAsync(c, applicationId, roleId, permissionId);
            await AssignUserRoleAsync(c, applicationId, userId, roleId);
            return (roleId, permissionId);
        }

        // Inactive UserRole assignment.
        {
            var (roleId, _) = await GrantRoleAsync(client, "userrole");
            await client.PostAsync($"/applications/{applicationId}/users/{userId}/roles/{roleId}/remove", null);
            using var response = await client.GetAsync($"/applications/{applicationId}/users/{userId}/effective-permissions");
            Assert.AreEqual(0, JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("items").GetArrayLength());
        }

        // Inactive role.
        {
            var (roleId, _) = await GrantRoleAsync(client, "role");
            await client.PostAsync($"/applications/{applicationId}/roles/{roleId}/deactivate", null);
            using var response = await client.GetAsync($"/applications/{applicationId}/users/{userId}/effective-permissions");
            Assert.AreEqual(0, JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("items").GetArrayLength());
            using var roleStillExists = await client.GetAsync($"/applications/{applicationId}/roles/{roleId}");
            Assert.AreEqual(HttpStatusCode.OK, roleStillExists.StatusCode);
        }

        // Inactive RolePermission.
        {
            var (roleId, permissionId) = await GrantRoleAsync(client, "rolepermission");
            await client.PostAsync($"/applications/{applicationId}/roles/{roleId}/permissions/{permissionId}/remove", null);
            using var response = await client.GetAsync($"/applications/{applicationId}/users/{userId}/effective-permissions");
            Assert.AreEqual(0, JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("items").GetArrayLength());
        }

        // Inactive permission.
        {
            var (_, permissionId) = await GrantRoleAsync(client, "permission");
            await client.PostAsync($"/applications/{applicationId}/permissions/{permissionId}/deactivate", null);
            using var response = await client.GetAsync($"/applications/{applicationId}/users/{userId}/effective-permissions");
            Assert.AreEqual(0, JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("items").GetArrayLength());
            using var permissionStillExists = await client.GetAsync($"/applications/{applicationId}/permissions/{permissionId}");
            Assert.AreEqual(HttpStatusCode.OK, permissionStillExists.StatusCode);
        }

        // Inactive application membership.
        {
            await GrantRoleAsync(client, "membership");
            await client.PostAsync($"/applications/{applicationId}/memberships/{userId}/deactivate", null);
            using var response = await client.GetAsync($"/applications/{applicationId}/users/{userId}/effective-permissions");
            Assert.AreEqual(0, JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("items").GetArrayLength());
            await client.PostAsync($"/applications/{applicationId}/memberships/{userId}/activate", null);
        }

        // Inactive user.
        {
            await GrantRoleAsync(client, "user");
            await client.PostAsync($"/users/{userId}/deactivate", null);
            using var response = await client.GetAsync($"/applications/{applicationId}/users/{userId}/effective-permissions");
            Assert.AreEqual(0, JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("items").GetArrayLength());
            await client.PostAsync($"/users/{userId}/activate", null);
        }

        // Inactive application.
        {
            await GrantRoleAsync(client, "application");
            await client.PostAsync($"/applications/{applicationId}/deactivate", null);
            using var response = await client.GetAsync($"/applications/{applicationId}/users/{userId}/effective-permissions");
            Assert.AreEqual(0, JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("items").GetArrayLength());
        }
    }

    // T036: isolation between applications, including after membership reactivation.
    [TestMethod]
    public async Task Effective_permissions_never_leak_across_applications_even_after_reactivation()
    {
        using var factory = await FactoryAsync(); using var client = factory.CreateClient();
        var userId = await CreateUserAsync(client);
        var firstApplicationId = await CreateApplicationAsync(client);
        var secondApplicationId = await CreateApplicationAsync(client);
        await CreateMembershipAsync(client, userId, firstApplicationId);
        await CreateMembershipAsync(client, userId, secondApplicationId);

        var firstRoleId = await CreateRoleAsync(client, firstApplicationId, "Role");
        var firstPermissionId = await CreatePermissionAsync(client, firstApplicationId, "first.permission");
        await AssignRolePermissionAsync(client, firstApplicationId, firstRoleId, firstPermissionId);
        await AssignUserRoleAsync(client, firstApplicationId, userId, firstRoleId);

        using var secondBeforeAnyGrant = await client.GetAsync($"/applications/{secondApplicationId}/users/{userId}/effective-permissions");
        Assert.AreEqual(0, JsonDocument.Parse(await secondBeforeAnyGrant.Content.ReadAsStringAsync()).RootElement.GetProperty("items").GetArrayLength());

        await client.PostAsync($"/applications/{secondApplicationId}/memberships/{userId}/deactivate", null);
        await client.PostAsync($"/applications/{secondApplicationId}/memberships/{userId}/activate", null);

        using var firstAfter = await client.GetAsync($"/applications/{firstApplicationId}/users/{userId}/effective-permissions");
        var firstCodes = JsonDocument.Parse(await firstAfter.Content.ReadAsStringAsync()).RootElement.GetProperty("items").EnumerateArray().Select(x => x.GetProperty("code").GetString()).ToArray();
        CollectionAssert.AreEqual(new[] { "first.permission" }, firstCodes);

        using var secondAfterReactivation = await client.GetAsync($"/applications/{secondApplicationId}/users/{userId}/effective-permissions");
        Assert.AreEqual(0, JsonDocument.Parse(await secondAfterReactivation.Content.ReadAsStringAsync()).RootElement.GetProperty("items").GetArrayLength());
    }

    private static async Task<Guid> CreateUserAsync(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync("/users", new { email = $"ep-{Guid.NewGuid():N}@example.test", password = "Quickstart!2026", firstName = "A", lastName = "B", displayName = "AB" });
        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreateApplicationAsync(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync("/applications", new { code = $"app-{Guid.NewGuid():N}", name = "Application" });
        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetGuid();
    }

    private static async Task CreateMembershipAsync(HttpClient client, Guid userId, Guid applicationId)
    {
        using var response = await client.PostAsJsonAsync($"/applications/{applicationId}/memberships", new { userId });
        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
    }

    private static async Task<Guid> CreateRoleAsync(HttpClient client, Guid applicationId, string name)
    {
        using var response = await client.PostAsJsonAsync($"/applications/{applicationId}/roles", new { name });
        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreatePermissionAsync(HttpClient client, Guid applicationId, string code)
    {
        using var response = await client.PostAsJsonAsync($"/applications/{applicationId}/permissions", new { code });
        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetGuid();
    }

    private static async Task AssignRolePermissionAsync(HttpClient client, Guid applicationId, Guid roleId, Guid permissionId)
    {
        using var response = await client.PostAsync($"/applications/{applicationId}/roles/{roleId}/permissions/{permissionId}", null);
        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
    }

    private static async Task AssignUserRoleAsync(HttpClient client, Guid applicationId, Guid userId, Guid roleId)
    {
        using var response = await client.PostAsync($"/applications/{applicationId}/users/{userId}/roles/{roleId}", null);
        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
    }

    private static async Task<WebApplicationFactory<Program>> FactoryAsync() { var factory = new WebApplicationFactory<Program>(); using var scope = factory.Services.CreateScope(); await scope.ServiceProvider.GetRequiredService<AuthenticationDbContext>().Database.MigrateAsync(); return factory; }
}
