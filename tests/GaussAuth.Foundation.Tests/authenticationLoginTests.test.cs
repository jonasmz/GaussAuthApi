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

    // T022: inactive user is rejected and remains inactive (no auto-reactivation).
    [TestMethod]
    public async Task Inactive_user_is_rejected_and_remains_inactive()
    {
        using var factory = await FactoryAsync(); using var client = factory.CreateClient();
        var email = $"login-{Guid.NewGuid():N}@example.test";
        var userId = await CreateUserAsync(client, email);
        var (applicationId, applicationCode) = await CreateApplicationAsync(client);
        await CreateMembershipAsync(client, userId, applicationId);

        using var deactivate = await client.PostAsync($"/users/{userId}/deactivate", null);
        Assert.AreEqual(HttpStatusCode.OK, deactivate.StatusCode);

        using var response = await client.PostAsJsonAsync("/auth/login", new { applicationCode, email, password = Password });
        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);

        using var userAfter = await client.GetAsync($"/users/{userId}");
        Assert.IsFalse(JsonDocument.Parse(await userAfter.Content.ReadAsStringAsync()).RootElement.GetProperty("isActive").GetBoolean());
    }

    // T023: nonexistent/inactive application is rejected; the membership record still exists; the application remains inactive.
    [TestMethod]
    public async Task Nonexistent_or_inactive_application_is_rejected_and_remains_inactive()
    {
        using var factory = await FactoryAsync(); using var client = factory.CreateClient();

        var unknownAppEmail = $"login-{Guid.NewGuid():N}@example.test";
        await CreateUserAsync(client, unknownAppEmail);
        using var unknownApp = await client.PostAsJsonAsync("/auth/login", new { applicationCode = $"no-such-app-{Guid.NewGuid():N}", email = unknownAppEmail, password = Password });
        Assert.AreEqual(HttpStatusCode.Unauthorized, unknownApp.StatusCode);

        var email = $"login-{Guid.NewGuid():N}@example.test";
        var userId = await CreateUserAsync(client, email);
        var (applicationId, applicationCode) = await CreateApplicationAsync(client);
        await CreateMembershipAsync(client, userId, applicationId);

        using var deactivateApp = await client.PostAsync($"/applications/{applicationId}/deactivate", null);
        Assert.AreEqual(HttpStatusCode.OK, deactivateApp.StatusCode);

        using var response = await client.PostAsJsonAsync("/auth/login", new { applicationCode, email, password = Password });
        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);

        using var membershipAfter = await client.GetAsync($"/applications/{applicationId}/memberships/{userId}");
        Assert.AreEqual(HttpStatusCode.OK, membershipAfter.StatusCode);

        using var appAfter = await client.GetAsync($"/applications/{applicationId}");
        Assert.IsFalse(JsonDocument.Parse(await appAfter.Content.ReadAsStringAsync()).RootElement.GetProperty("isActive").GetBoolean());
    }

    // T024: missing/inactive membership is rejected; an inactive membership remains inactive.
    [TestMethod]
    public async Task Missing_or_inactive_membership_is_rejected_and_remains_inactive()
    {
        using var factory = await FactoryAsync(); using var client = factory.CreateClient();

        var missingMembershipEmail = $"login-{Guid.NewGuid():N}@example.test";
        await CreateUserAsync(client, missingMembershipEmail);
        var (noMembershipAppId, noMembershipAppCode) = await CreateApplicationAsync(client);
        using var missingMembership = await client.PostAsJsonAsync("/auth/login", new { applicationCode = noMembershipAppCode, email = missingMembershipEmail, password = Password });
        Assert.AreEqual(HttpStatusCode.Unauthorized, missingMembership.StatusCode);

        var email = $"login-{Guid.NewGuid():N}@example.test";
        var userId = await CreateUserAsync(client, email);
        var (applicationId, applicationCode) = await CreateApplicationAsync(client);
        await CreateMembershipAsync(client, userId, applicationId);

        using var deactivateMembership = await client.PostAsync($"/applications/{applicationId}/memberships/{userId}/deactivate", null);
        Assert.AreEqual(HttpStatusCode.OK, deactivateMembership.StatusCode);

        using var response = await client.PostAsJsonAsync("/auth/login", new { applicationCode, email, password = Password });
        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);

        using var membershipAfter = await client.GetAsync($"/applications/{applicationId}/memberships/{userId}");
        Assert.IsFalse(JsonDocument.Parse(await membershipAfter.Content.ReadAsStringAsync()).RootElement.GetProperty("isActive").GetBoolean());
    }

    // T025: a membership active only in application A never grants login to application B.
    [TestMethod]
    public async Task Membership_in_one_application_does_not_grant_login_to_another()
    {
        using var factory = await FactoryAsync(); using var client = factory.CreateClient();
        var email = $"login-{Guid.NewGuid():N}@example.test";
        var userId = await CreateUserAsync(client, email);
        var (applicationAId, applicationACode) = await CreateApplicationAsync(client);
        var (_, applicationBCode) = await CreateApplicationAsync(client);
        await CreateMembershipAsync(client, userId, applicationAId);

        using var loginA = await client.PostAsJsonAsync("/auth/login", new { applicationCode = applicationACode, email, password = Password });
        Assert.AreEqual(HttpStatusCode.OK, loginA.StatusCode);

        using var loginB = await client.PostAsJsonAsync("/auth/login", new { applicationCode = applicationBCode, email, password = Password });
        Assert.AreEqual(HttpStatusCode.Unauthorized, loginB.StatusCode);
    }

    // T026: repeated invalid-password attempts reach lockout; a subsequent correct-password attempt still fails.
    [TestMethod]
    public async Task Lockout_rejects_correct_password_once_threshold_is_reached()
    {
        using var factory = await FactoryAsync(new Dictionary<string, string?>
        {
            ["Identity:Lockout:MaxFailedAccessAttempts"] = "3",
            ["RateLimiting:Login:PermitLimit"] = "50"
        });
        using var client = factory.CreateClient();
        var email = $"login-{Guid.NewGuid():N}@example.test";
        var userId = await CreateUserAsync(client, email);
        var (applicationId, applicationCode) = await CreateApplicationAsync(client);
        await CreateMembershipAsync(client, userId, applicationId);

        for (var i = 0; i < 3; i++)
        {
            using var attempt = await client.PostAsJsonAsync("/auth/login", new { applicationCode, email, password = "WrongPassword!2026" });
            Assert.AreEqual(HttpStatusCode.Unauthorized, attempt.StatusCode);
        }

        using var lockedOutAttempt = await client.PostAsJsonAsync("/auth/login", new { applicationCode, email, password = Password });
        Assert.AreEqual(HttpStatusCode.Unauthorized, lockedOutAttempt.StatusCode);
    }

    // T027: requests exceeding the configured rate limit are rejected with 429 and Retry-After.
    [TestMethod]
    public async Task Rate_limit_rejects_requests_beyond_the_configured_window()
    {
        using var factory = await FactoryAsync(new Dictionary<string, string?>
        {
            ["RateLimiting:Login:PermitLimit"] = "3",
            ["RateLimiting:Login:WindowSeconds"] = "60"
        });
        using var client = factory.CreateClient();
        var email = $"login-{Guid.NewGuid():N}@example.test";
        var userId = await CreateUserAsync(client, email);
        var (applicationId, applicationCode) = await CreateApplicationAsync(client);
        await CreateMembershipAsync(client, userId, applicationId);

        for (var i = 0; i < 3; i++)
        {
            using var attempt = await client.PostAsJsonAsync("/auth/login", new { applicationCode, email, password = Password });
            Assert.AreEqual(HttpStatusCode.OK, attempt.StatusCode);
        }

        using var rateLimited = await client.PostAsJsonAsync("/auth/login", new { applicationCode, email, password = Password });
        Assert.AreEqual(HttpStatusCode.TooManyRequests, rateLimited.StatusCode);
        Assert.IsTrue(rateLimited.Headers.Contains("Retry-After"));
    }

    // T028: every uniform-failure cause (including lockout) produces an identical 401 shape.
    [TestMethod]
    public async Task All_uniform_failure_causes_produce_an_identical_response_shape()
    {
        using var factory = await FactoryAsync(); using var client = factory.CreateClient();

        var unknownEmail = await LoginAsync(client, await CreateApplicationAsync(client), $"no-such-{Guid.NewGuid():N}@example.test", Password);

        var wrongPasswordEmail = $"login-{Guid.NewGuid():N}@example.test";
        var wrongPasswordUserId = await CreateUserAsync(client, wrongPasswordEmail);
        var wrongPasswordApp = await CreateApplicationAsync(client);
        await CreateMembershipAsync(client, wrongPasswordUserId, wrongPasswordApp.Id);
        var wrongPassword = await LoginAsync(client, wrongPasswordApp, wrongPasswordEmail, "WrongPassword!2026");

        var inactiveUserEmail = $"login-{Guid.NewGuid():N}@example.test";
        var inactiveUserId = await CreateUserAsync(client, inactiveUserEmail);
        var inactiveUserApp = await CreateApplicationAsync(client);
        await CreateMembershipAsync(client, inactiveUserId, inactiveUserApp.Id);
        await client.PostAsync($"/users/{inactiveUserId}/deactivate", null);
        var inactiveUser = await LoginAsync(client, inactiveUserApp, inactiveUserEmail, Password);

        var inactiveAppEmail = $"login-{Guid.NewGuid():N}@example.test";
        var inactiveAppUserId = await CreateUserAsync(client, inactiveAppEmail);
        var inactiveApp = await CreateApplicationAsync(client);
        await CreateMembershipAsync(client, inactiveAppUserId, inactiveApp.Id);
        await client.PostAsync($"/applications/{inactiveApp.Id}/deactivate", null);
        var inactiveApplication = await LoginAsync(client, inactiveApp, inactiveAppEmail, Password);

        var missingMembershipEmail = $"login-{Guid.NewGuid():N}@example.test";
        await CreateUserAsync(client, missingMembershipEmail);
        var missingMembershipApp = await CreateApplicationAsync(client);
        var missingMembership = await LoginAsync(client, missingMembershipApp, missingMembershipEmail, Password);

        using var lockoutFactory = await FactoryAsync(new Dictionary<string, string?>
        {
            ["Identity:Lockout:MaxFailedAccessAttempts"] = "1",
            ["RateLimiting:Login:PermitLimit"] = "50"
        });
        using var lockoutClient = lockoutFactory.CreateClient();
        var lockoutEmail = $"login-{Guid.NewGuid():N}@example.test";
        var lockoutUserId = await CreateUserAsync(lockoutClient, lockoutEmail);
        var (lockoutAppId, lockoutAppCode) = await CreateApplicationAsync(lockoutClient);
        await CreateMembershipAsync(lockoutClient, lockoutUserId, lockoutAppId);
        await lockoutClient.PostAsJsonAsync("/auth/login", new { applicationCode = lockoutAppCode, email = lockoutEmail, password = "WrongPassword!2026" });
        using var lockout = await lockoutClient.PostAsJsonAsync("/auth/login", new { applicationCode = lockoutAppCode, email = lockoutEmail, password = Password });

        var bodies = new[] { unknownEmail, wrongPassword, inactiveUser, inactiveApplication, missingMembership, await lockout.Content.ReadAsStringAsync() };
        foreach (var body in bodies.Skip(1))
        {
            AssertSameProblemShape(bodies[0], body);
        }
        Assert.AreEqual(HttpStatusCode.Unauthorized, lockout.StatusCode);
    }

    private static async Task<string> LoginAsync(HttpClient client, (Guid Id, string Code) application, string email, string password)
    {
        using var response = await client.PostAsJsonAsync("/auth/login", new { applicationCode = application.Code, email, password });
        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
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

    // Managed as environment variables (not ConfigureAppConfiguration) because WebApplicationFactory's
    // host-building interception does not reliably apply extra configuration sources in time for the
    // minimal-hosting Program.cs's own eager configuration.GetValue(...) calls; environment variables,
    // read by the default AddEnvironmentVariables() source, are visible as soon as they are set in this
    // (in-process) test process. Every known key is always set explicitly to avoid bleed-through between
    // tests (MSTest runs this class's methods sequentially, so no concurrent mutation occurs).
    private static readonly string[] ManagedEnvironmentKeys =
    [
        "Identity__Lockout__MaxFailedAccessAttempts",
        "Identity__Lockout__DefaultLockoutMinutes",
        "RateLimiting__Login__PermitLimit",
        "RateLimiting__Login__WindowSeconds"
    ];

    private static async Task<WebApplicationFactory<Program>> FactoryAsync(Dictionary<string, string?>? configOverrides = null)
    {
        foreach (var key in ManagedEnvironmentKeys)
        {
            Environment.SetEnvironmentVariable(key, configOverrides?.GetValueOrDefault(key.Replace("__", ":")));
        }

        var factory = new WebApplicationFactory<Program>();
        using var scope = factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AuthenticationDbContext>().Database.MigrateAsync();
        return factory;
    }
}
