using GaussAuth.Application.Passwords.Ports;
using GaussAuth.Infrastructure.Passwords;
using GaussAuth.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace GaussAuth.Foundation.Tests;

[TestClass]
public sealed class PasswordManagementTests
{
    private const string InitialPassword = "Quickstart!2026";

    [TestMethod]
    public async Task Change_password_requires_current_password_and_revokes_every_session()
    {
        using var factory = await FactoryAsync(); using var client = factory.CreateClient();
        var login = await CreateLoginAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);

        using var rejected = await client.PostAsJsonAsync("/auth/password/change", new { currentPassword = "incorrect", newPassword = "Updated!2026" });
        Assert.AreEqual(HttpStatusCode.BadRequest, rejected.StatusCode);
        using var changed = await client.PostAsJsonAsync("/auth/password/change", new { currentPassword = InitialPassword, newPassword = "Updated!2026" });
        Assert.AreEqual(HttpStatusCode.NoContent, changed.StatusCode);

        using var oldSession = await client.PostAsJsonAsync("/auth/session/validate", new { applicationCode = login.ApplicationCode });
        Assert.AreEqual(HttpStatusCode.Unauthorized, oldSession.StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            var sessions = await scope.ServiceProvider.GetRequiredService<AuthenticationDbContext>().Sessions.Where(item => item.UserId == login.UserId).ToListAsync();
            Assert.IsTrue(sessions.Count > 0 && sessions.All(item => item.RevokedAt is not null));
        }
        client.DefaultRequestHeaders.Authorization = null;
        using var oldLogin = await client.PostAsJsonAsync("/auth/login", new { applicationCode = login.ApplicationCode, email = login.Email, password = InitialPassword });
        using var newLogin = await client.PostAsJsonAsync("/auth/login", new { applicationCode = login.ApplicationCode, email = login.Email, password = "Updated!2026" });
        Assert.AreEqual(HttpStatusCode.Unauthorized, oldLogin.StatusCode);
        Assert.AreEqual(HttpStatusCode.OK, newLogin.StatusCode);
    }

    [TestMethod]
    public async Task Recovery_is_generic_for_known_unknown_and_inactive_users_and_delivers_only_for_eligible_user()
    {
        var path = Path.Combine(Path.GetTempPath(), ".recovery", $"{Guid.NewGuid():N}.jsonl");
        Environment.SetEnvironmentVariable("PasswordRecovery__DeliveryFile", path);
        try
        {
            using var factory = await FactoryAsync(); using var client = factory.CreateClient();
            var eligible = $"eligible-{Guid.NewGuid():N}@example.test";
            var inactive = $"inactive-{Guid.NewGuid():N}@example.test";
            var eligibleId = await CreateUserAsync(client, eligible);
            var inactiveId = await CreateUserAsync(client, inactive);
            using var deactivated = await client.PostAsync($"/users/{inactiveId}/deactivate", null);
            Assert.AreEqual(HttpStatusCode.OK, deactivated.StatusCode);

            var responses = new List<string>();
            foreach (var email in new[] { eligible, $"unknown-{Guid.NewGuid():N}@example.test", inactive })
            {
                using var response = await client.PostAsJsonAsync("/auth/password/recovery", new { email });
                Assert.AreEqual(HttpStatusCode.Accepted, response.StatusCode);
                responses.Add(await response.Content.ReadAsStringAsync());
            }
            Assert.IsTrue(responses.All(body => body == responses[0]));
            var lines = await File.ReadAllLinesAsync(path);
            Assert.AreEqual(1, lines.Length);
            Assert.IsTrue(lines[0].Contains(eligibleId.ToString(), StringComparison.Ordinal));
        }
        finally
        {
            Environment.SetEnvironmentVariable("PasswordRecovery__DeliveryFile", null);
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [TestMethod]
    public async Task Change_password_rejects_policy_failures_and_inactive_users_without_changing_memberships()
    {
        using var factory = await FactoryAsync(); using var client = factory.CreateClient();
        var login = await CreateLoginAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);
        using var policyRejected = await client.PostAsJsonAsync("/auth/password/change", new { currentPassword = InitialPassword, newPassword = "short" });
        Assert.AreEqual(HttpStatusCode.BadRequest, policyRejected.StatusCode);
        using var deactivated = await client.PostAsync($"/users/{login.UserId}/deactivate", null);
        Assert.AreEqual(HttpStatusCode.OK, deactivated.StatusCode);
        using var inactiveRejected = await client.PostAsJsonAsync("/auth/password/change", new { currentPassword = InitialPassword, newPassword = "Updated!2026" });
        Assert.AreEqual(HttpStatusCode.BadRequest, inactiveRejected.StatusCode);
        using var memberships = await client.GetAsync($"/users/{login.UserId}/memberships");
        Assert.AreEqual(HttpStatusCode.OK, memberships.StatusCode);
        Assert.IsTrue(JsonDocument.Parse(await memberships.Content.ReadAsStringAsync()).RootElement.GetProperty("items").GetArrayLength() == 1);
    }

    [TestMethod]
    public async Task Recovery_endpoint_is_rate_limited()
    {
        Environment.SetEnvironmentVariable("RateLimiting__PasswordRecovery__PermitLimit", "1");
        try
        {
            using var factory = await FactoryAsync(); using var client = factory.CreateClient();
            using var first = await client.PostAsJsonAsync("/auth/password/recovery", new { email = "first@example.test" });
            using var second = await client.PostAsJsonAsync("/auth/password/recovery", new { email = "second@example.test" });
            Assert.AreEqual(HttpStatusCode.Accepted, first.StatusCode);
            Assert.AreEqual((HttpStatusCode)429, second.StatusCode);
        }
        finally { Environment.SetEnvironmentVariable("RateLimiting__PasswordRecovery__PermitLimit", null); }
    }

    [TestMethod]
    public async Task Development_delivery_writes_only_to_configured_ignored_recovery_file()
    {
        var path = Path.Combine(Path.GetTempPath(), ".recovery", $"{Guid.NewGuid():N}.jsonl");
        try
        {
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["PasswordRecovery:DeliveryFile"] = path }).Build();
            var delivery = new ProtectedFileRecoveryDelivery(configuration, new TestHostEnvironment("Development"));
            await delivery.DeliverAsync(new RecoveryDeliveryInstruction(Guid.NewGuid(), "user@example.test", "test-credential"), CancellationToken.None);
            Assert.IsTrue(File.Exists(path));
            Assert.IsTrue(await File.ReadAllTextAsync(path) is { Length: > 0 });
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [TestMethod]
    public async Task Delivery_fails_without_writing_a_credential_outside_development_or_testing()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.jsonl");
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["PasswordRecovery:DeliveryFile"] = path }).Build();
        var delivery = new ProtectedFileRecoveryDelivery(configuration, new TestHostEnvironment("Production"));
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => delivery.DeliverAsync(new RecoveryDeliveryInstruction(Guid.NewGuid(), "user@example.test", "test-credential"), CancellationToken.None));
        Assert.IsFalse(File.Exists(path));
    }

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "tests";
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private static async Task<WebApplicationFactory<Program>> FactoryAsync()
    {
        var factory = new WebApplicationFactory<Program>();
        using var scope = factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AuthenticationDbContext>().Database.MigrateAsync();
        return factory;
    }

    private static async Task<LoginResult> CreateLoginAsync(HttpClient client)
    {
        var email = $"password-{Guid.NewGuid():N}@example.test";
        var userId = await CreateUserAsync(client, email);
        var applicationCode = $"app-{Guid.NewGuid():N}";
        using var application = await client.PostAsJsonAsync("/applications", new { code = applicationCode, name = "Application" });
        var applicationId = JsonDocument.Parse(await application.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetGuid();
        using var membership = await client.PostAsJsonAsync($"/applications/{applicationId}/memberships", new { userId });
        using var login = await client.PostAsJsonAsync("/auth/login", new { applicationCode, email, password = InitialPassword });
        Assert.AreEqual(HttpStatusCode.OK, login.StatusCode);
        var body = JsonDocument.Parse(await login.Content.ReadAsStringAsync()).RootElement;
        return new LoginResult(userId, applicationCode, email, body.GetProperty("accessToken").GetString()!);
    }

    private static async Task<Guid> CreateUserAsync(HttpClient client, string email)
    {
        using var response = await client.PostAsJsonAsync("/users", new { email, password = InitialPassword, firstName = "A", lastName = "B", displayName = "AB" });
        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetGuid();
    }

    private sealed record LoginResult(Guid UserId, string ApplicationCode, string Email, string AccessToken);
}
