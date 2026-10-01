using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GaussAuth.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GaussAuth.Foundation.Tests;

[TestClass]
public sealed class UserActivationTests
{
    [TestMethod]
    public async Task Deactivating_an_active_user_changes_state_and_preserves_the_record()
    {
        using var factory = await CreateMigratedFactoryAsync();
        using var client = factory.CreateClient();

        var email = UniqueEmail("deactivate");
        using var createResponse = await client.PostAsJsonAsync("/users", ValidCreateRequest(email));
        var location = createResponse.Headers.Location!;

        using var response = await client.PostAsync(location.ToString() + "/deactivate", content: null);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.IsFalse(document.RootElement.GetProperty("isActive").GetBoolean());

        using var getResponse = await client.GetAsync(location);
        Assert.AreEqual(HttpStatusCode.OK, getResponse.StatusCode);
        using var getDocument = JsonDocument.Parse(await getResponse.Content.ReadAsStringAsync());
        Assert.IsFalse(getDocument.RootElement.GetProperty("isActive").GetBoolean());
        Assert.AreEqual("Quick", getDocument.RootElement.GetProperty("profile").GetProperty("firstName").GetString());
    }

    [TestMethod]
    public async Task Deactivating_an_already_inactive_user_is_idempotent_and_unchanged()
    {
        using var factory = await CreateMigratedFactoryAsync();
        using var client = factory.CreateClient();

        var email = UniqueEmail("deactivate-twice");
        using var createResponse = await client.PostAsJsonAsync("/users", ValidCreateRequest(email));
        var location = createResponse.Headers.Location!;

        using var first = await client.PostAsync(location.ToString() + "/deactivate", content: null);
        Assert.AreEqual(HttpStatusCode.OK, first.StatusCode);

        using var getAfterFirst = await client.GetAsync(location);
        using var getDocument = JsonDocument.Parse(await getAfterFirst.Content.ReadAsStringAsync());
        var firstUpdatedAt = getDocument.RootElement.GetProperty("updatedAt").GetDateTimeOffset();

        using var second = await client.PostAsync(location.ToString() + "/deactivate", content: null);

        Assert.AreEqual(HttpStatusCode.OK, second.StatusCode);
        using var secondDocument = JsonDocument.Parse(await second.Content.ReadAsStringAsync());
        Assert.IsFalse(secondDocument.RootElement.GetProperty("isActive").GetBoolean());
        Assert.AreEqual(firstUpdatedAt, secondDocument.RootElement.GetProperty("updatedAt").GetDateTimeOffset());
    }

    [TestMethod]
    public async Task Reactivating_an_inactive_user_returns_to_active_state()
    {
        using var factory = await CreateMigratedFactoryAsync();
        using var client = factory.CreateClient();

        var email = UniqueEmail("reactivate");
        using var createResponse = await client.PostAsJsonAsync("/users", ValidCreateRequest(email));
        var location = createResponse.Headers.Location!;

        using var deactivate = await client.PostAsync(location.ToString() + "/deactivate", content: null);
        Assert.AreEqual(HttpStatusCode.OK, deactivate.StatusCode);

        using var activate = await client.PostAsync(location.ToString() + "/activate", content: null);

        Assert.AreEqual(HttpStatusCode.OK, activate.StatusCode);
        using var document = JsonDocument.Parse(await activate.Content.ReadAsStringAsync());
        Assert.IsTrue(document.RootElement.GetProperty("isActive").GetBoolean());
    }

    [TestMethod]
    public async Task Reactivating_an_already_active_user_is_idempotent_and_unchanged()
    {
        using var factory = await CreateMigratedFactoryAsync();
        using var client = factory.CreateClient();

        var email = UniqueEmail("activate-twice");
        using var createResponse = await client.PostAsJsonAsync("/users", ValidCreateRequest(email));
        var location = createResponse.Headers.Location!;

        using var first = await client.PostAsync(location.ToString() + "/activate", content: null);
        using var firstDocument = JsonDocument.Parse(await first.Content.ReadAsStringAsync());
        var firstUpdatedAt = firstDocument.RootElement.GetProperty("updatedAt").GetDateTimeOffset();

        using var second = await client.PostAsync(location.ToString() + "/activate", content: null);

        Assert.AreEqual(HttpStatusCode.OK, second.StatusCode);
        using var secondDocument = JsonDocument.Parse(await second.Content.ReadAsStringAsync());
        Assert.IsTrue(secondDocument.RootElement.GetProperty("isActive").GetBoolean());
        Assert.AreEqual(firstUpdatedAt, secondDocument.RootElement.GetProperty("updatedAt").GetDateTimeOffset());
    }

    [TestMethod]
    public async Task Unknown_identifier_on_either_route_returns_not_found()
    {
        using var factory = await CreateMigratedFactoryAsync();
        using var client = factory.CreateClient();

        var unknownId = Guid.NewGuid();

        using var activateResponse = await client.PostAsync($"/users/{unknownId}/activate", content: null);
        using var deactivateResponse = await client.PostAsync($"/users/{unknownId}/deactivate", content: null);

        Assert.AreEqual(HttpStatusCode.NotFound, activateResponse.StatusCode);
        Assert.AreEqual(HttpStatusCode.NotFound, deactivateResponse.StatusCode);
    }

    private static async Task<WebApplicationFactory<Program>> CreateMigratedFactoryAsync()
    {
        var factory = new WebApplicationFactory<Program>();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuthenticationDbContext>();
        await db.Database.MigrateAsync();
        return factory;
    }

    private static string UniqueEmail(string label) => $"{label}-{Guid.NewGuid():N}@example.test";

    private static object ValidCreateRequest(
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
