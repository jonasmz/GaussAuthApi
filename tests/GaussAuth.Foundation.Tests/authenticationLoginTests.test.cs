using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GaussAuth.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GaussAuth.Foundation.Tests;

[TestClass]
public sealed class AuthenticationLoginTests
{
    private const string Password = "Quickstart!2026";

    // T011: successful login with active user/application/membership and correct password; no roles/permissions assigned (FR-010).
    [TestMethod]
    public async Task Successful_login_returns_stable_identifiers()
    {
        using var factory = await FactoryAsync(); using var client = factory.CreateClient();
        var email = $"login-{Guid.NewGuid():N}@example.test";
        var userId = await CreateUserAsync(client, email);
        var (applicationId, applicationCode) = await CreateApplicationAsync(client);
        await CreateMembershipAsync(client, userId, applicationId);

        using var response = await client.PostAsJsonAsync("/auth/login", new { applicationCode, email, password = Password });
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.AreEqual(userId, body.GetProperty("userId").GetGuid());
        Assert.AreEqual(applicationId, body.GetProperty("applicationId").GetGuid());
    }

    // T012: normalized-email login (case/whitespace variants resolve to the same identity).
    [TestMethod]
    public async Task Login_normalizes_email_before_resolving_identity()
    {
        using var factory = await FactoryAsync(); using var client = factory.CreateClient();
        var email = $"login-{Guid.NewGuid():N}@example.test";
        var userId = await CreateUserAsync(client, email);
        var (applicationId, applicationCode) = await CreateApplicationAsync(client);
        await CreateMembershipAsync(client, userId, applicationId);

        var variantEmail = $"  {email.ToUpperInvariant()}  ";
        using var response = await client.PostAsJsonAsync("/auth/login", new { applicationCode, email = variantEmail, password = Password });
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.AreEqual(userId, body.GetProperty("userId").GetGuid());
    }

    // T013: no credential content anywhere in a successful response.
    [TestMethod]
    public async Task Successful_login_response_contains_no_credential_content()
    {
        using var factory = await FactoryAsync(); using var client = factory.CreateClient();
        var email = $"login-{Guid.NewGuid():N}@example.test";
        var userId = await CreateUserAsync(client, email);
        var (applicationId, applicationCode) = await CreateApplicationAsync(client);
        await CreateMembershipAsync(client, userId, applicationId);

        using var response = await client.PostAsJsonAsync("/auth/login", new { applicationCode, email, password = Password });
        var raw = await response.Content.ReadAsStringAsync();
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        StringAssert.DoesNotMatch(raw, new System.Text.RegularExpressions.Regex("password", System.Text.RegularExpressions.RegexOptions.IgnoreCase));
    }

    // T019: unknown email returns the uniform failure.
    [TestMethod]
    public async Task Unknown_email_returns_uniform_failure()
    {
        using var factory = await FactoryAsync(); using var client = factory.CreateClient();
        var (_, applicationCode) = await CreateApplicationAsync(client);

        using var response = await client.PostAsJsonAsync("/auth/login", new { applicationCode, email = $"no-such-{Guid.NewGuid():N}@example.test", password = Password });
        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // T020: wrong password returns a failure identical to T019's unknown-email outcome.
    [TestMethod]
    public async Task Wrong_password_returns_failure_identical_to_unknown_email()
    {
        using var factory = await FactoryAsync(); using var client = factory.CreateClient();
        var email = $"login-{Guid.NewGuid():N}@example.test";
        var userId = await CreateUserAsync(client, email);
        var (applicationId, applicationCode) = await CreateApplicationAsync(client);
        await CreateMembershipAsync(client, userId, applicationId);

        using var unknownEmail = await client.PostAsJsonAsync("/auth/login", new { applicationCode, email = $"no-such-{Guid.NewGuid():N}@example.test", password = Password });
        using var wrongPassword = await client.PostAsJsonAsync("/auth/login", new { applicationCode, email, password = "WrongPassword!2026" });

        Assert.AreEqual(unknownEmail.StatusCode, wrongPassword.StatusCode);
        AssertSameProblemShape(await unknownEmail.Content.ReadAsStringAsync(), await wrongPassword.Content.ReadAsStringAsync());
    }

    // T021: invalid input is rejected with 400 and never echoes the password.
    [TestMethod]
    public async Task Invalid_input_is_rejected_without_echoing_password()
    {
        using var factory = await FactoryAsync(); using var client = factory.CreateClient();

        using var missingEmail = await client.PostAsJsonAsync("/auth/login", new { applicationCode = "some-app", password = Password });
        Assert.AreEqual(HttpStatusCode.BadRequest, missingEmail.StatusCode);

        using var missingPassword = await client.PostAsJsonAsync("/auth/login", new { applicationCode = "some-app", email = "user@example.test" });
        Assert.AreEqual(HttpStatusCode.BadRequest, missingPassword.StatusCode);

        using var malformedEmail = await client.PostAsJsonAsync("/auth/login", new { applicationCode = "some-app", email = "not-an-email", password = Password });
        Assert.AreEqual(HttpStatusCode.BadRequest, malformedEmail.StatusCode);

        var secretPassword = $"Secret!{Guid.NewGuid():N}";
        using var tooLongApplicationCode = await client.PostAsJsonAsync("/auth/login", new { applicationCode = new string('a', 65), email = "user@example.test", password = secretPassword });
        Assert.AreEqual(HttpStatusCode.BadRequest, tooLongApplicationCode.StatusCode);
        var raw = await tooLongApplicationCode.Content.ReadAsStringAsync();
        StringAssert.DoesNotMatch(raw, new System.Text.RegularExpressions.Regex(System.Text.RegularExpressions.Regex.Escape(secretPassword)));
    }

    private static void AssertSameProblemShape(string first, string second)
    {
        var firstBody = JsonDocument.Parse(first).RootElement;
        var secondBody = JsonDocument.Parse(second).RootElement;
        Assert.AreEqual(firstBody.GetProperty("type").GetString(), secondBody.GetProperty("type").GetString());
        Assert.AreEqual(firstBody.GetProperty("title").GetString(), secondBody.GetProperty("title").GetString());
        Assert.AreEqual(firstBody.GetProperty("status").GetInt32(), secondBody.GetProperty("status").GetInt32());
        Assert.AreEqual(firstBody.EnumerateObject().Count(), secondBody.EnumerateObject().Count());
    }

    private static async Task<Guid> CreateUserAsync(HttpClient client, string email)
    {
        using var response = await client.PostAsJsonAsync("/users", new { email, password = Password, firstName = "A", lastName = "B", displayName = "AB" });
        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<(Guid Id, string Code)> CreateApplicationAsync(HttpClient client)
    {
        var code = $"app-{Guid.NewGuid():N}";
        using var response = await client.PostAsJsonAsync("/applications", new { code, name = "Application" });
        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
        var id = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetGuid();
        return (id, code);
    }

    private static async Task CreateMembershipAsync(HttpClient client, Guid userId, Guid applicationId)
    {
        using var response = await client.PostAsJsonAsync($"/applications/{applicationId}/memberships", new { userId });
        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
    }

    private static async Task<WebApplicationFactory<Program>> FactoryAsync() { var factory = new WebApplicationFactory<Program>(); using var scope = factory.Services.CreateScope(); await scope.ServiceProvider.GetRequiredService<AuthenticationDbContext>().Database.MigrateAsync(); return factory; }
}
