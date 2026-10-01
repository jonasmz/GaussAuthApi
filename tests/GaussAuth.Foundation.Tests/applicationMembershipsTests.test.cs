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
    private static async Task<WebApplicationFactory<Program>> FactoryAsync() { var factory = new WebApplicationFactory<Program>(); using var scope = factory.Services.CreateScope(); await scope.ServiceProvider.GetRequiredService<AuthenticationDbContext>().Database.MigrateAsync(); return factory; }
}
