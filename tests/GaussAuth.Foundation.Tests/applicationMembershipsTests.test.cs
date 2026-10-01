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
    private static async Task<WebApplicationFactory<Program>> FactoryAsync() { var factory = new WebApplicationFactory<Program>(); using var scope = factory.Services.CreateScope(); await scope.ServiceProvider.GetRequiredService<AuthenticationDbContext>().Database.MigrateAsync(); return factory; }
}
