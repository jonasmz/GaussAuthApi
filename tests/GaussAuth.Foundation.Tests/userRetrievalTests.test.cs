using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GaussAuth.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GaussAuth.Foundation.Tests;

[TestClass]
public sealed class UserRetrievalTests
{
    [TestMethod]
    public async Task Existing_user_is_retrieved_without_exposing_credentials()
    {
        using var factory = await CreateMigratedFactoryAsync();
        using var clientAdmin = await factory.CreateAdminClientAsync(); var client = clientAdmin.Client;

        var email = UniqueEmail("retrieve");
        using var createResponse = await client.PostAsJsonAsync("/admin/users", ValidRequest(email));
        Assert.AreEqual(HttpStatusCode.Created, createResponse.StatusCode);
        var location = createResponse.Headers.Location!;

        using var response = await client.GetAsync(location);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("password", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("securityStamp", body, StringComparison.OrdinalIgnoreCase);

        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        Assert.AreEqual(email, root.GetProperty("email").GetString());
        Assert.IsTrue(root.GetProperty("isActive").GetBoolean());
        Assert.AreEqual("Quick", root.GetProperty("profile").GetProperty("firstName").GetString());
        Assert.IsTrue(root.TryGetProperty("id", out _));
        Assert.IsTrue(root.TryGetProperty("normalizedEmail", out _));
        Assert.IsTrue(root.TryGetProperty("createdAt", out _));
        Assert.IsTrue(root.TryGetProperty("updatedAt", out _));
    }

    [TestMethod]
    public async Task Unknown_identifier_returns_not_found_without_revealing_internals()
    {
        using var factory = await CreateMigratedFactoryAsync();
        using var clientAdmin = await factory.CreateAdminClientAsync(); var client = clientAdmin.Client;

        using var response = await client.GetAsync($"/admin/users/{Guid.NewGuid()}");

        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static async Task<WebApplicationFactory<Program>> CreateMigratedFactoryAsync()
    {
        var factory = new WebApplicationFactory<Program>().WithGlobalAdministrators();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuthenticationDbContext>();
        await db.Database.MigrateAsync();
        return factory;
    }

    private static string UniqueEmail(string label) => $"{label}-{Guid.NewGuid():N}@example.test";

    private static object ValidRequest(
        string email,
        string password = "Quickstart!2026",
        string firstName = "Quick") => new
    {
        email,
        password,
        firstName,
        lastName = "Start",
        displayName = "Quick Start",
        phoneNumber = (string?)null
    };
}
