using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GaussAuth.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GaussAuth.Foundation.Tests;

[TestClass]
public sealed class ApplicationMembershipsTests
{
    [TestMethod]
    public async Task Active_user_can_join_active_application_once()
    {
        using var factory = await FactoryAsync(); using var client = factory.CreateClient();
        using var user = await client.PostAsJsonAsync("/users", new { email = $"membership-{Guid.NewGuid():N}@example.test", password = "Quickstart!2026", firstName = "A", lastName = "B", displayName = "AB" });
        using var app = await client.PostAsJsonAsync("/applications", new { code = $"app-{Guid.NewGuid():N}", name = "Application" });
        var userId = JsonDocument.Parse(await user.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetGuid();
        var appId = JsonDocument.Parse(await app.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetGuid();
        using var membership = await client.PostAsJsonAsync($"/applications/{appId}/memberships", new { userId });
        Assert.AreEqual(HttpStatusCode.Created, membership.StatusCode);
        using var duplicate = await client.PostAsJsonAsync($"/applications/{appId}/memberships", new { userId });
        Assert.AreEqual(HttpStatusCode.Conflict, duplicate.StatusCode);
    }

    [TestMethod]
    public async Task Membership_queries_and_parent_state_rules_are_enforced()
    {
        using var factory = await FactoryAsync(); using var client = factory.CreateClient();
        var userId = await CreateUserAsync(client);
        var applicationId = await CreateApplicationAsync(client);
        using var membership = await client.PostAsJsonAsync($"/applications/{applicationId}/memberships", new { userId });
        using var pair = await client.GetAsync($"/applications/{applicationId}/memberships/{userId}");
        using var applicationList = await client.GetAsync($"/applications/{applicationId}/memberships?limit=1");
        using var userList = await client.GetAsync($"/users/{userId}/memberships?limit=1");
        using var badList = await client.GetAsync($"/applications/{applicationId}/memberships?cursor=not-a-guid");
        using var missingUser = await client.PostAsJsonAsync($"/applications/{applicationId}/memberships", new { userId = Guid.NewGuid() });
        using var deactivateApplication = await client.PostAsync($"/applications/{applicationId}/deactivate", null);
        using var inactiveApplication = await client.PostAsJsonAsync($"/applications/{applicationId}/memberships", new { userId = await CreateUserAsync(client) });
        Assert.AreEqual(HttpStatusCode.Created, membership.StatusCode);
        Assert.AreEqual(HttpStatusCode.OK, pair.StatusCode);
        Assert.AreEqual(HttpStatusCode.OK, applicationList.StatusCode);
        Assert.AreEqual(HttpStatusCode.OK, userList.StatusCode);
        Assert.AreEqual(HttpStatusCode.BadRequest, badList.StatusCode);
        Assert.AreEqual(HttpStatusCode.NotFound, missingUser.StatusCode);
        Assert.AreEqual(HttpStatusCode.OK, deactivateApplication.StatusCode);
        Assert.AreEqual(HttpStatusCode.Conflict, inactiveApplication.StatusCode);
    }

    [TestMethod]
    public async Task Inactive_user_membership_is_preserved_as_inactive()
    {
        using var factory = await FactoryAsync(); using var client = factory.CreateClient();
        var userId = await CreateUserAsync(client);
        var applicationId = await CreateApplicationAsync(client);
        using var deactivateUser = await client.PostAsync($"/users/{userId}/deactivate", null);
        using var membership = await client.PostAsJsonAsync($"/applications/{applicationId}/memberships", new { userId });
        var body = JsonDocument.Parse(await membership.Content.ReadAsStringAsync()).RootElement;
        Assert.AreEqual(HttpStatusCode.OK, deactivateUser.StatusCode);
        Assert.AreEqual(HttpStatusCode.Created, membership.StatusCode);
        Assert.IsFalse(body.GetProperty("isActive").GetBoolean());
    }

    [TestMethod]
    public async Task Membership_lifecycle_is_idempotent_and_respects_inactive_parents()
    {
        using var factory = await FactoryAsync(); using var client = factory.CreateClient();
        var userId = await CreateUserAsync(client);
        var applicationId = await CreateApplicationAsync(client);
        await CreateMembershipAsync(client, userId, applicationId);
        using var deactivate = await client.PostAsync($"/applications/{applicationId}/memberships/{userId}/deactivate", null);
        using var repeatedDeactivate = await client.PostAsync($"/applications/{applicationId}/memberships/{userId}/deactivate", null);
        using var activate = await client.PostAsync($"/applications/{applicationId}/memberships/{userId}/activate", null);
        using var deactivateUser = await client.PostAsync($"/users/{userId}/deactivate", null);
        using var inactiveUserActivation = await client.PostAsync($"/applications/{applicationId}/memberships/{userId}/activate", null);
        using var afterInactiveUser = await client.GetAsync($"/applications/{applicationId}/memberships/{userId}");
        using var activateUser = await client.PostAsync($"/users/{userId}/activate", null);
        using var deactivateApplication = await client.PostAsync($"/applications/{applicationId}/deactivate", null);
        using var inactiveApplicationActivation = await client.PostAsync($"/applications/{applicationId}/memberships/{userId}/activate", null);
        using var afterInactiveApplication = await client.GetAsync($"/applications/{applicationId}/memberships/{userId}");
        using var missing = await client.PostAsync($"/applications/{applicationId}/memberships/{Guid.NewGuid()}/activate", null);
        Assert.AreEqual(HttpStatusCode.OK, deactivate.StatusCode);
        Assert.AreEqual(HttpStatusCode.OK, repeatedDeactivate.StatusCode);
        Assert.AreEqual(HttpStatusCode.OK, activate.StatusCode);
        Assert.AreEqual(HttpStatusCode.OK, deactivateUser.StatusCode);
        Assert.AreEqual(HttpStatusCode.Conflict, inactiveUserActivation.StatusCode);
        Assert.IsTrue(JsonDocument.Parse(await afterInactiveUser.Content.ReadAsStringAsync()).RootElement.GetProperty("isActive").GetBoolean());
        Assert.AreEqual(HttpStatusCode.OK, activateUser.StatusCode);
        Assert.AreEqual(HttpStatusCode.OK, deactivateApplication.StatusCode);
        Assert.AreEqual(HttpStatusCode.Conflict, inactiveApplicationActivation.StatusCode);
        Assert.IsTrue(JsonDocument.Parse(await afterInactiveApplication.Content.ReadAsStringAsync()).RootElement.GetProperty("isActive").GetBoolean());
        Assert.AreEqual(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [TestMethod]
    public async Task Membership_state_is_isolated_between_applications_and_parent_deactivation_preserves_it()
    {
        using var factory = await FactoryAsync(); using var client = factory.CreateClient();
        var userId = await CreateUserAsync(client);
        var firstApplicationId = await CreateApplicationAsync(client);
        var secondApplicationId = await CreateApplicationAsync(client);
        await CreateMembershipAsync(client, userId, firstApplicationId);
        await CreateMembershipAsync(client, userId, secondApplicationId);
        using var deactivateFirst = await client.PostAsync($"/applications/{firstApplicationId}/memberships/{userId}/deactivate", null);
        using var secondMembership = await client.GetAsync($"/applications/{secondApplicationId}/memberships/{userId}");
        using var deactivateSecondApplication = await client.PostAsync($"/applications/{secondApplicationId}/deactivate", null);
        using var preservedSecondMembership = await client.GetAsync($"/applications/{secondApplicationId}/memberships/{userId}");
        Assert.AreEqual(HttpStatusCode.OK, deactivateFirst.StatusCode);
        Assert.IsTrue(JsonDocument.Parse(await secondMembership.Content.ReadAsStringAsync()).RootElement.GetProperty("isActive").GetBoolean());
        Assert.AreEqual(HttpStatusCode.OK, deactivateSecondApplication.StatusCode);
        Assert.IsTrue(JsonDocument.Parse(await preservedSecondMembership.Content.ReadAsStringAsync()).RootElement.GetProperty("isActive").GetBoolean());
    }

    private static async Task<Guid> CreateUserAsync(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync("/users", new { email = $"membership-{Guid.NewGuid():N}@example.test", password = "Quickstart!2026", firstName = "A", lastName = "B", displayName = "AB" });
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
    private static async Task<WebApplicationFactory<Program>> FactoryAsync() { var factory = new WebApplicationFactory<Program>(); using var scope = factory.Services.CreateScope(); await scope.ServiceProvider.GetRequiredService<AuthenticationDbContext>().Database.MigrateAsync(); return factory; }
}
