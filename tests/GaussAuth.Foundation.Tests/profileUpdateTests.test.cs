using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GaussAuth.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GaussAuth.Foundation.Tests;

[TestClass]
public sealed class ProfileUpdateTests
{
    [TestMethod]
    public async Task Valid_update_changes_only_profile_fields()
    {
        using var factory = await CreateMigratedFactoryAsync();
        using var client = factory.CreateClient();

        var email = UniqueEmail("update");
        using var createResponse = await client.PostAsJsonAsync("/users", ValidCreateRequest(email));
        Assert.AreEqual(HttpStatusCode.Created, createResponse.StatusCode);
        var location = createResponse.Headers.Location!;

        using var response = await client.PutAsJsonAsync(location.ToString() + "/profile", ValidUpdateRequest());

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();

        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        Assert.AreEqual(email, root.GetProperty("email").GetString());
        Assert.IsTrue(root.GetProperty("isActive").GetBoolean());
        var profile = root.GetProperty("profile");
        Assert.AreEqual("Updated", profile.GetProperty("firstName").GetString());
        Assert.AreEqual("Name", profile.GetProperty("lastName").GetString());
        Assert.AreEqual("Updated Name", profile.GetProperty("displayName").GetString());

        using var getResponse = await client.GetAsync(location);
        using var getDocument = JsonDocument.Parse(await getResponse.Content.ReadAsStringAsync());
        var createdAt = getDocument.RootElement.GetProperty("profile").GetProperty("createdAt").GetDateTimeOffset();
        var updatedAt = profile.GetProperty("updatedAt").GetDateTimeOffset();
        Assert.IsTrue(updatedAt >= createdAt);
    }

    [TestMethod]
    public async Task Invalid_field_is_rejected_and_prior_profile_values_unchanged()
    {
        using var factory = await CreateMigratedFactoryAsync();
        using var client = factory.CreateClient();

        var email = UniqueEmail("update-invalid");
        using var createResponse = await client.PostAsJsonAsync("/users", ValidCreateRequest(email));
        var location = createResponse.Headers.Location!;

        using var response = await client.PutAsJsonAsync(
            location.ToString() + "/profile", ValidUpdateRequest(firstName: string.Empty));

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);

        using var getResponse = await client.GetAsync(location);
        using var document = JsonDocument.Parse(await getResponse.Content.ReadAsStringAsync());
        Assert.AreEqual("Quick", document.RootElement.GetProperty("profile").GetProperty("firstName").GetString());
    }

    [TestMethod]
    public async Task Request_with_email_field_is_rejected_and_login_email_remains_immutable()
    {
        using var factory = await CreateMigratedFactoryAsync();
        using var client = factory.CreateClient();

        var email = UniqueEmail("update-email-attempt");
        using var createResponse = await client.PostAsJsonAsync("/users", ValidCreateRequest(email));
        var location = createResponse.Headers.Location!;

        using var response = await client.PutAsJsonAsync(location.ToString() + "/profile", new
        {
            email = "someone-else@example.test",
            firstName = "Updated",
            lastName = "Name",
            displayName = "Updated Name",
            phoneNumber = (string?)null,
            avatarReference = (string?)null
        });

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);

        using var getResponse = await client.GetAsync(location);
        using var document = JsonDocument.Parse(await getResponse.Content.ReadAsStringAsync());
        Assert.AreEqual(email, document.RootElement.GetProperty("email").GetString());
        Assert.AreEqual("Quick", document.RootElement.GetProperty("profile").GetProperty("firstName").GetString());
    }

    [TestMethod]
    public async Task Unknown_identifier_returns_not_found()
    {
        using var factory = await CreateMigratedFactoryAsync();
        using var client = factory.CreateClient();

        using var response = await client.PutAsJsonAsync($"/users/{Guid.NewGuid()}/profile", ValidUpdateRequest());

        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
    }

    [TestMethod]
    public async Task Sequential_updates_demonstrate_last_write_wins()
    {
        using var factory = await CreateMigratedFactoryAsync();
        using var client = factory.CreateClient();

        var email = UniqueEmail("update-sequential");
        using var createResponse = await client.PostAsJsonAsync("/users", ValidCreateRequest(email));
        var location = createResponse.Headers.Location!;

        using var firstUpdate = await client.PutAsJsonAsync(
            location.ToString() + "/profile", ValidUpdateRequest(firstName: "First"));
        Assert.AreEqual(HttpStatusCode.OK, firstUpdate.StatusCode);

        using var secondUpdate = await client.PutAsJsonAsync(
            location.ToString() + "/profile", ValidUpdateRequest(firstName: "Second"));
        Assert.AreEqual(HttpStatusCode.OK, secondUpdate.StatusCode);

        using var getResponse = await client.GetAsync(location);
        using var document = JsonDocument.Parse(await getResponse.Content.ReadAsStringAsync());
        Assert.AreEqual("Second", document.RootElement.GetProperty("profile").GetProperty("firstName").GetString());
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
        phoneNumber = (string?)null,
        avatarReference = (string?)null
    };

    private static object ValidUpdateRequest(
        string firstName = "Updated",
        string lastName = "Name",
        string displayName = "Updated Name") => new
    {
        firstName,
        lastName,
        displayName,
        phoneNumber = (string?)null,
        avatarReference = (string?)null
    };
}
