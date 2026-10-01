using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using GaussAuth.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace GaussAuth.Foundation.Tests;

[TestClass]
public sealed class SessionsAccessTests
{
    private const string Password = "Quickstart!2026";

    [TestMethod]
    public async Task Login_creates_session_with_minimal_signed_credential()
    {
        using var factory = await FactoryAsync(); using var client = factory.CreateClient();
        var login = await CreateLoginAsync(client);
        Assert.AreEqual("Bearer", login.TokenType);
        Assert.IsTrue(login.CacheNoStore);

        var payload = DecodeJson(login.AccessToken.Split('.')[1]);
        CollectionAssert.AreEquivalent(new[] { "iss", "sub", "aud", "sid", "iat", "exp" }, payload.EnumerateObject().Select(x => x.Name).ToArray());
        Assert.AreEqual(login.UserId, payload.GetProperty("sub").GetGuid());
        Assert.AreEqual(login.ApplicationId, payload.GetProperty("aud").GetGuid());
        Assert.AreEqual(login.SessionId, payload.GetProperty("sid").GetGuid());

        using var scope = factory.Services.CreateScope();
        var session = await scope.ServiceProvider.GetRequiredService<AuthenticationDbContext>().Sessions.SingleAsync(x => x.Id == login.SessionId);
        Assert.AreEqual(login.UserId, session.UserId); Assert.AreEqual(login.ApplicationId, session.ApplicationId);
        Assert.IsNull(session.RevokedAt); Assert.AreEqual(session.CreatedAt.AddHours(8), session.ExpiresAt);
    }

    [TestMethod]
    public async Task Validate_renew_and_signing_keys_follow_the_contract()
    {
        using var factory = await FactoryAsync(); using var client = factory.CreateClient();
        var login = await CreateLoginAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);

        using var validated = await client.PostAsJsonAsync("/auth/session/validate", new { applicationCode = login.ApplicationCode });
        Assert.AreEqual(HttpStatusCode.OK, validated.StatusCode);
        var context = JsonDocument.Parse(await validated.Content.ReadAsStringAsync()).RootElement;
        Assert.AreEqual(login.SessionId, context.GetProperty("sessionId").GetGuid());

        using var renewed = await client.PostAsync("/auth/session/renew", null);
        Assert.AreEqual(HttpStatusCode.OK, renewed.StatusCode);
        Assert.IsTrue(renewed.Headers.CacheControl?.NoStore);
        var renewedBody = JsonDocument.Parse(await renewed.Content.ReadAsStringAsync()).RootElement;
        Assert.AreEqual(login.SessionId, renewedBody.GetProperty("sessionId").GetGuid());

        using var keys = await client.GetAsync("/auth/signing-keys");
        Assert.AreEqual(HttpStatusCode.OK, keys.StatusCode);
        Assert.AreEqual("public, max-age=300", keys.Headers.CacheControl?.ToString());
        var key = JsonDocument.Parse(await keys.Content.ReadAsStringAsync()).RootElement.GetProperty("keys")[0];
        Assert.AreEqual("EC", key.GetProperty("kty").GetString()); Assert.AreEqual("ES256", key.GetProperty("alg").GetString());
        Assert.IsFalse(key.TryGetProperty("d", out _));
    }

    [TestMethod]
    public async Task Invalid_credentials_return_uniform_bearer_unauthorized()
    {
        using var factory = await FactoryAsync(); using var client = factory.CreateClient();
        var login = await CreateLoginAsync(client);
        var cases = new[] { "not-a-token", login.AccessToken[..^1] + (login.AccessToken[^1] == 'a' ? "b" : "a") };
        string? baseline = null;
        foreach (var credential in cases)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/auth/session/validate") { Content = JsonContent.Create(new { applicationCode = login.ApplicationCode }) };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", credential);
            using var response = await client.SendAsync(request);
            Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.AreEqual("Bearer", response.Headers.WwwAuthenticate.Single().Scheme);
            baseline ??= await response.Content.ReadAsStringAsync();
            Assert.AreEqual(baseline, await response.Content.ReadAsStringAsync());
        }
    }

    [TestMethod]
    public async Task Logout_is_durable_idempotent_and_rejects_future_access()
    {
        using var factory = await FactoryAsync(); using var client = factory.CreateClient();
        var login = await CreateLoginAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);

        using var logout = await client.PostAsync("/auth/logout", null);
        Assert.AreEqual(HttpStatusCode.NoContent, logout.StatusCode);
        using var secondLogout = await client.PostAsync("/auth/logout", null);
        Assert.AreEqual(HttpStatusCode.NoContent, secondLogout.StatusCode);
        using var validate = await client.PostAsJsonAsync("/auth/session/validate", new { applicationCode = login.ApplicationCode });
        using var renew = await client.PostAsync("/auth/session/renew", null);
        Assert.AreEqual(HttpStatusCode.Unauthorized, validate.StatusCode);
        Assert.AreEqual(HttpStatusCode.Unauthorized, renew.StatusCode);

        using var scope = factory.Services.CreateScope();
        var session = await scope.ServiceProvider.GetRequiredService<AuthenticationDbContext>().Sessions.SingleAsync(item => item.Id == login.SessionId);
        Assert.IsNotNull(session.RevokedAt);
    }

    [TestMethod]
    public async Task Sessions_are_independent_and_application_scoped()
    {
        using var factory = await FactoryAsync(); using var client = factory.CreateClient();
        var email = $"isolation-{Guid.NewGuid():N}@example.test";
        var userId = await CreateUserAsync(client, email);
        var appA = await CreateApplicationAsync(client); var appB = await CreateApplicationAsync(client);
        await CreateMembershipAsync(client, userId, appA.Id); await CreateMembershipAsync(client, userId, appB.Id);
        var first = await LoginExistingAsync(client, appA.Code, email);
        var second = await LoginExistingAsync(client, appA.Code, email);
        var third = await LoginExistingAsync(client, appB.Code, email);
        Assert.AreNotEqual(first.SessionId, second.SessionId); Assert.AreNotEqual(first.SessionId, third.SessionId);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", first.AccessToken);
        using var wrongApplication = await client.PostAsJsonAsync("/auth/session/validate", new { applicationCode = appB.Code });
        Assert.AreEqual(HttpStatusCode.Unauthorized, wrongApplication.StatusCode);
        using var logout = await client.PostAsync("/auth/logout", null);
        Assert.AreEqual(HttpStatusCode.NoContent, logout.StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", second.AccessToken);
        using var secondValidation = await client.PostAsJsonAsync("/auth/session/validate", new { applicationCode = appA.Code });
        Assert.AreEqual(HttpStatusCode.OK, secondValidation.StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", third.AccessToken);
        using var thirdValidation = await client.PostAsJsonAsync("/auth/session/validate", new { applicationCode = appB.Code });
        Assert.AreEqual(HttpStatusCode.OK, thirdValidation.StatusCode);
    }

    private static async Task<LoginResult> CreateLoginAsync(HttpClient client)
    {
        var email = $"sessions-{Guid.NewGuid():N}@example.test";
        var userId = await CreateUserAsync(client, email);
        var (applicationId, applicationCode) = await CreateApplicationAsync(client);
        await CreateMembershipAsync(client, userId, applicationId);
        using var response = await client.PostAsJsonAsync("/auth/login", new { applicationCode, email, password = Password, userId = Guid.NewGuid() });
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        return new LoginResult(response.Headers.CacheControl?.NoStore == true, userId, applicationId, applicationCode, body.GetProperty("sessionId").GetGuid(), body.GetProperty("accessToken").GetString()!, body.GetProperty("tokenType").GetString()!);
    }

    private static async Task<LoginResult> LoginExistingAsync(HttpClient client, string applicationCode, string email)
    {
        using var response = await client.PostAsJsonAsync("/auth/login", new { applicationCode, email, password = Password });
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        return new LoginResult(response.Headers.CacheControl?.NoStore == true, body.GetProperty("userId").GetGuid(), body.GetProperty("applicationId").GetGuid(), applicationCode, body.GetProperty("sessionId").GetGuid(), body.GetProperty("accessToken").GetString()!, body.GetProperty("tokenType").GetString()!);
    }

    private static JsonElement DecodeJson(string base64Url)
    {
        var padded = base64Url.Replace('-', '+').Replace('_', '/');
        padded = padded.PadRight(padded.Length + (4 - padded.Length % 4) % 4, '=');
        return JsonDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(padded))).RootElement.Clone();
    }

    private static async Task<Guid> CreateUserAsync(HttpClient client, string email)
    {
        using var response = await client.PostAsJsonAsync("/users", new { email, password = Password, firstName = "A", lastName = "B", displayName = "AB" });
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<(Guid Id, string Code)> CreateApplicationAsync(HttpClient client)
    {
        var code = $"app-{Guid.NewGuid():N}";
        using var response = await client.PostAsJsonAsync("/applications", new { code, name = "Application" });
        return (JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetGuid(), code);
    }

    private static async Task CreateMembershipAsync(HttpClient client, Guid userId, Guid applicationId)
    {
        using var response = await client.PostAsJsonAsync($"/applications/{applicationId}/memberships", new { userId });
        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
    }

    private static async Task<WebApplicationFactory<Program>> FactoryAsync()
    {
        foreach (var key in ManagedEnvironmentKeys) Environment.SetEnvironmentVariable(key, null);
        var time = new MutableTimeProvider();
        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(time);
        }));
        using var scope = factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AuthenticationDbContext>().Database.MigrateAsync();
        return factory;
    }

    private static readonly string[] ManagedEnvironmentKeys =
    ["RateLimiting__Login__PermitLimit", "Sessions__SessionLifetimeMinutes", "Sessions__AccessTokenLifetimeMinutes", "Sessions__Issuer", "RateLimiting__SigningKeys__PermitLimit", "RateLimiting__SigningKeys__WindowSeconds", "RateLimiting__SessionCredentials__PermitLimit", "RateLimiting__SessionCredentials__WindowSeconds"];

    private sealed record LoginResult(bool CacheNoStore, Guid UserId, Guid ApplicationId, string ApplicationCode, Guid SessionId, string AccessToken, string TokenType);
}
