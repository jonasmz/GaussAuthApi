using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GaussAuth.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GaussAuth.Foundation.Tests;

[TestClass]
public sealed class CreateUserTests
{
    [TestMethod]
    public async Task Valid_request_creates_user_and_profile_without_exposing_credentials()
    {
        using var factory = await CreateMigratedFactoryAsync();
        using var client = factory.CreateClient();

        var email = UniqueEmail("create");
        using var response = await client.PostAsJsonAsync("/users", ValidRequest(email));

        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
        Assert.IsNotNull(response.Headers.Location);

        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("password", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("securityStamp", body, StringComparison.OrdinalIgnoreCase);

        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        Assert.AreEqual(email, root.GetProperty("email").GetString());
        Assert.IsTrue(root.GetProperty("isActive").GetBoolean());
        Assert.AreEqual("Quick", root.GetProperty("profile").GetProperty("firstName").GetString());
    }

    [TestMethod]
    public async Task Duplicate_normalized_email_is_rejected()
    {
        using var factory = await CreateMigratedFactoryAsync();
        using var client = factory.CreateClient();

        var email = UniqueEmail("dup");
        using var first = await client.PostAsJsonAsync("/users", ValidRequest(email));
        Assert.AreEqual(HttpStatusCode.Created, first.StatusCode);

        var differentCasingAndWhitespace = $" {email.ToUpperInvariant()} ";
        using var second = await client.PostAsJsonAsync("/users", ValidRequest(differentCasingAndWhitespace));

        Assert.AreEqual(HttpStatusCode.Conflict, second.StatusCode);
    }

    [TestMethod]
    public async Task Malformed_email_is_rejected_with_field_level_validation_error()
    {
        using var factory = await CreateMigratedFactoryAsync();
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync("/users", ValidRequest("not-an-email"));

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"email\"", body, StringComparison.OrdinalIgnoreCase);
    }

    [TestMethod]
    public async Task Missing_required_field_is_rejected_without_partial_state()
    {
        using var factory = await CreateMigratedFactoryAsync();
        using var client = factory.CreateClient();

        var email = UniqueEmail("missing-field");
        using var response = await client.PostAsJsonAsync(
            "/users", ValidRequest(email, firstName: string.Empty));

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);

        using var retry = await client.PostAsJsonAsync("/users", ValidRequest(email));
        Assert.AreEqual(HttpStatusCode.Created, retry.StatusCode);
    }

    [TestMethod]
    public async Task Password_failing_identity_policy_is_rejected_without_partial_state()
    {
        using var factory = await CreateMigratedFactoryAsync();
        using var client = factory.CreateClient();

        var email = UniqueEmail("weak-password");
        using var response = await client.PostAsJsonAsync(
            "/users", ValidRequest(email, password: "a"));

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);

        using var retry = await client.PostAsJsonAsync("/users", ValidRequest(email));
        Assert.AreEqual(HttpStatusCode.Created, retry.StatusCode);
    }

    [TestMethod]
    public async Task Concurrent_requests_for_the_same_normalized_email_create_exactly_one_user()
    {
        using var factory = await CreateMigratedFactoryAsync();
        using var clientA = factory.CreateClient();
        using var clientB = factory.CreateClient();

        var email = UniqueEmail("race");
        var requestA = clientA.PostAsJsonAsync("/users", ValidRequest(email));
        var requestB = clientB.PostAsJsonAsync("/users", ValidRequest(email));

        var responses = await Task.WhenAll(requestA, requestB);

        try
        {
            var statusCodes = responses.Select(response => response.StatusCode).OrderBy(code => code).ToArray();
            CollectionAssert.AreEqual(
                new[] { HttpStatusCode.Created, HttpStatusCode.Conflict }.OrderBy(code => code).ToArray(),
                statusCodes);
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }
    }

    [TestMethod]
    public async Task Exceeding_the_rate_limit_rejects_further_creation_requests()
    {
        var migrated = await CreateMigratedFactoryAsync();
        using var factory = migrated.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("RateLimiting:UserCreation:PermitLimit", "2");
            builder.UseSetting("RateLimiting:UserCreation:WindowSeconds", "60");
        });
        using var client = factory.CreateClient();

        using var first = await client.PostAsJsonAsync("/users", ValidRequest(UniqueEmail("rate-1")));
        using var second = await client.PostAsJsonAsync("/users", ValidRequest(UniqueEmail("rate-2")));
        using var third = await client.PostAsJsonAsync("/users", ValidRequest(UniqueEmail("rate-3")));

        Assert.AreEqual(HttpStatusCode.Created, first.StatusCode);
        Assert.AreEqual(HttpStatusCode.Created, second.StatusCode);
        Assert.AreEqual(HttpStatusCode.TooManyRequests, third.StatusCode);
        Assert.IsTrue(third.Headers.Contains("Retry-After"));
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
