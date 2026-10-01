using GaussAuth.Application.AuthorizationContext.Ports;
using GaussAuth.Application.Security;
using GaussAuth.Infrastructure.AuthorizationContext;
using GaussAuth.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace GaussAuth.Foundation.Tests;

[TestClass]
public sealed class AuthorizationContractTests
{
    private const string Password = "Quickstart!2026";

    [TestMethod]
    public async Task Consumer_credential_is_bound_to_its_configured_application_and_allows_rotation()
    {
        var validator = CreateValidator(new Dictionary<string, string?>
        {
            ["AuthorizationConsumers:orders:CurrentSecret"] = "orders-current-secret",
            ["AuthorizationConsumers:orders:RetiringSecret"] = "orders-retiring-secret",
            ["AuthorizationConsumers:billing:CurrentSecret"] = "billing-current-secret"
        });

        Assert.IsTrue((await validator.ValidateAsync("orders", "orders-current-secret", CancellationToken.None)).IsValid);
        Assert.IsTrue((await validator.ValidateAsync("orders", "orders-retiring-secret", CancellationToken.None)).IsValid);
        Assert.IsFalse((await validator.ValidateAsync("billing", "orders-current-secret", CancellationToken.None)).IsValid);
        Assert.IsFalse((await validator.ValidateAsync("orders", "billing-current-secret", CancellationToken.None)).IsValid);

        var afterRetiringCredentialRemoval = CreateValidator(new Dictionary<string, string?>
        {
            ["AuthorizationConsumers:orders:CurrentSecret"] = "orders-current-secret"
        });
        Assert.IsTrue((await afterRetiringCredentialRemoval.ValidateAsync("orders", "orders-current-secret", CancellationToken.None)).IsValid);
        Assert.IsFalse((await afterRetiringCredentialRemoval.ValidateAsync("orders", "orders-retiring-secret", CancellationToken.None)).IsValid);
    }

    [TestMethod]
    public void Consumer_configuration_rejects_secret_reuse_without_exposing_the_secret()
    {
        var exception = Assert.ThrowsExactly<InvalidOperationException>(() => CreateValidator(new Dictionary<string, string?>
        {
            ["AuthorizationConsumers:orders:CurrentSecret"] = "shared-secret",
            ["AuthorizationConsumers:billing:CurrentSecret"] = "shared-secret"
        }));

        Assert.IsFalse(exception.Message.Contains("shared-secret", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Consumer_configuration_fails_during_startup_validation_when_a_current_secret_is_invalid()
    {
        var exception = Assert.ThrowsExactly<InvalidOperationException>(() => CreateValidator(new Dictionary<string, string?>
        {
            ["AuthorizationConsumers:orders:CurrentSecret"] = " "
        }));

        Assert.AreEqual("Authorization consumer configuration is invalid.", exception.Message);
    }

    [TestMethod]
    public void Authorization_context_events_are_safe_categorical_values()
    {
        Assert.IsTrue(Enum.IsDefined(SecurityEventType.AuthorizationContextResolved));
        Assert.IsTrue(Enum.IsDefined(SecurityEventType.AuthorizationContextRejected));
    }

    [TestMethod]
    public async Task Context_returns_only_current_application_identity_roles_and_permissions()
    {
        var applicationCode = $"orders-{Guid.NewGuid():N}";
        var serviceSecret = $"orders-service-{Guid.NewGuid():N}";
        using var factory = await FactoryAsync(new Dictionary<string, string?>
        {
            [$"AuthorizationConsumers:{applicationCode}:CurrentSecret"] = serviceSecret
        });
        using var client = factory.CreateClient();
        var user = await CreateUserAsync(client);
        var applicationId = await CreateApplicationAsync(client, applicationCode);
        await CreateMembershipAsync(client, applicationId, user.Id);
        var roleId = await CreateRoleAsync(client, applicationId, "operator");
        var permissionId = await CreatePermissionAsync(client, applicationId, "orders.create");
        await AssignAsync(client, $"/applications/{applicationId}/roles/{roleId}/permissions/{permissionId}");
        await AssignAsync(client, $"/applications/{applicationId}/users/{user.Id}/roles/{roleId}");
        var login = await LoginAsync(client, applicationCode, user.Email);

        using var response = await ResolveAsync(client, login.AccessToken, applicationCode, serviceSecret);
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.IsTrue(response.Headers.CacheControl?.NoStore);
        var context = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        CollectionAssert.AreEquivalent(new[] { "userId", "applicationId", "sessionId", "issuedAt", "credentialExpiresAt", "sessionExpiresAt", "roles", "permissions" }, context.EnumerateObject().Select(item => item.Name).ToArray());
        Assert.AreEqual(user.Id, context.GetProperty("userId").GetGuid());
        Assert.AreEqual(applicationId, context.GetProperty("applicationId").GetGuid());
        Assert.AreEqual(login.SessionId, context.GetProperty("sessionId").GetGuid());
        Assert.AreEqual(roleId, context.GetProperty("roles")[0].GetProperty("id").GetGuid());
        Assert.AreEqual("operator", context.GetProperty("roles")[0].GetProperty("name").GetString());
        CollectionAssert.AreEqual(new[] { "orders.create" }, context.GetProperty("permissions").EnumerateArray().Select(item => item.GetString()).ToArray());
    }

    [TestMethod]
    public async Task Context_rejects_invalid_and_cross_application_credentials_without_leaking_context()
    {
        var applicationA = $"orders-{Guid.NewGuid():N}";
        var applicationB = $"billing-{Guid.NewGuid():N}";
        var secretA = $"orders-service-{Guid.NewGuid():N}";
        var secretB = $"billing-service-{Guid.NewGuid():N}";
        using var factory = await FactoryAsync(new Dictionary<string, string?>
        {
            [$"AuthorizationConsumers:{applicationA}:CurrentSecret"] = secretA,
            [$"AuthorizationConsumers:{applicationB}:CurrentSecret"] = secretB
        });
        using var client = factory.CreateClient();
        var user = await CreateUserAsync(client);
        var applicationAId = await CreateApplicationAsync(client, applicationA);
        var applicationBId = await CreateApplicationAsync(client, applicationB);
        await CreateMembershipAsync(client, applicationAId, user.Id);
        await CreateMembershipAsync(client, applicationBId, user.Id);
        var roleA = await CreateRoleAsync(client, applicationAId, "orders-role");
        var permissionA = await CreatePermissionAsync(client, applicationAId, "orders.read");
        await AssignAsync(client, $"/applications/{applicationAId}/roles/{roleA}/permissions/{permissionA}");
        await AssignAsync(client, $"/applications/{applicationAId}/users/{user.Id}/roles/{roleA}");
        var roleB = await CreateRoleAsync(client, applicationBId, "billing-role");
        var permissionB = await CreatePermissionAsync(client, applicationBId, "billing.read");
        await AssignAsync(client, $"/applications/{applicationBId}/roles/{roleB}/permissions/{permissionB}");
        await AssignAsync(client, $"/applications/{applicationBId}/users/{user.Id}/roles/{roleB}");
        var login = await LoginAsync(client, applicationA, user.Email);

        using (var current = await ResolveAsync(client, login.AccessToken, applicationA, secretA))
        {
            Assert.AreEqual(HttpStatusCode.OK, current.StatusCode);
            var context = JsonDocument.Parse(await current.Content.ReadAsStringAsync()).RootElement;
            CollectionAssert.AreEqual(new[] { "orders.read" }, context.GetProperty("permissions").EnumerateArray().Select(item => item.GetString()).ToArray());
            CollectionAssert.AreEqual(new[] { "orders-role" }, context.GetProperty("roles").EnumerateArray().Select(item => item.GetProperty("name").GetString()).ToArray());
        }

        foreach (var (code, secret) in new[]
                 {
                     (applicationA, secretB),
                     (applicationB, secretA),
                     (applicationB, secretB),
                     (applicationA, "invalid-service-secret")
                 })
        {
            using var rejected = await ResolveAsync(client, login.AccessToken, code, secret);
            await AssertUnauthorizedAsync(rejected);
        }

        using (var deactivated = await client.PostAsync($"/users/{user.Id}/deactivate", null))
        {
            Assert.AreEqual(HttpStatusCode.OK, deactivated.StatusCode);
        }
        using (var inactive = await ResolveAsync(client, login.AccessToken, applicationA, secretA))
        {
            await AssertUnauthorizedAsync(inactive);
        }
        using (var activated = await client.PostAsync($"/users/{user.Id}/activate", null))
        {
            Assert.AreEqual(HttpStatusCode.OK, activated.StatusCode);
        }
        using (var membershipDeactivated = await client.PostAsync($"/applications/{applicationAId}/memberships/{user.Id}/deactivate", null))
        {
            Assert.AreEqual(HttpStatusCode.OK, membershipDeactivated.StatusCode);
        }
        using (var inactiveMembership = await ResolveAsync(client, login.AccessToken, applicationA, secretA))
        {
            await AssertUnauthorizedAsync(inactiveMembership);
        }
        using (var membershipActivated = await client.PostAsync($"/applications/{applicationAId}/memberships/{user.Id}/activate", null))
        {
            Assert.AreEqual(HttpStatusCode.OK, membershipActivated.StatusCode);
        }
        using (var applicationDeactivated = await client.PostAsync($"/applications/{applicationAId}/deactivate", null))
        {
            Assert.AreEqual(HttpStatusCode.OK, applicationDeactivated.StatusCode);
        }
        using (var inactiveApplication = await ResolveAsync(client, login.AccessToken, applicationA, secretA))
        {
            await AssertUnauthorizedAsync(inactiveApplication);
        }
        using (var applicationActivated = await client.PostAsync($"/applications/{applicationAId}/activate", null))
        {
            Assert.AreEqual(HttpStatusCode.OK, applicationActivated.StatusCode);
        }

        using (var logout = await LogoutAsync(client, login.AccessToken))
        {
            Assert.AreEqual(HttpStatusCode.NoContent, logout.StatusCode);
        }
        using var revoked = await ResolveAsync(client, login.AccessToken, applicationA, secretA);
        await AssertUnauthorizedAsync(revoked);
    }

    [TestMethod]
    public async Task Context_endpoint_uses_the_configured_rate_limit()
    {
        var applicationCode = $"rate-limited-{Guid.NewGuid():N}";
        var serviceSecret = $"rate-limited-service-{Guid.NewGuid():N}";
        using var factory = await FactoryAsync(new Dictionary<string, string?>
        {
            [$"AuthorizationConsumers:{applicationCode}:CurrentSecret"] = serviceSecret,
            ["RateLimiting:AuthorizationContext:PermitLimit"] = "1",
            ["RateLimiting:AuthorizationContext:WindowSeconds"] = "60"
        });
        using var client = factory.CreateClient();
        using var first = await ResolveAsync(client, "invalid-user-credential", applicationCode, serviceSecret);
        using var second = await ResolveAsync(client, "invalid-user-credential", applicationCode, serviceSecret);
        await AssertUnauthorizedAsync(first);
        Assert.AreEqual((HttpStatusCode)429, second.StatusCode);
    }

    [TestMethod]
    public async Task Context_rejects_an_expired_access_credential()
    {
        var applicationCode = $"expiring-{Guid.NewGuid():N}";
        var serviceSecret = $"expiring-service-{Guid.NewGuid():N}";
        var time = new MutableTimeProvider();
        using var factory = await FactoryAsync(new Dictionary<string, string?>
        {
            [$"AuthorizationConsumers:{applicationCode}:CurrentSecret"] = serviceSecret
        }, time);
        using var client = factory.CreateClient();
        var user = await CreateUserAsync(client);
        var applicationId = await CreateApplicationAsync(client, applicationCode);
        await CreateMembershipAsync(client, applicationId, user.Id);
        var login = await LoginAsync(client, applicationCode, user.Email);
        time.Advance(TimeSpan.FromMinutes(16));

        using var expired = await ResolveAsync(client, login.AccessToken, applicationCode, serviceSecret);
        await AssertUnauthorizedAsync(expired);
    }

    [TestMethod]
    public async Task Consumer_permission_check_uses_only_the_public_context_and_reflects_current_authorization()
    {
        var applicationCode = $"freshness-{Guid.NewGuid():N}";
        var serviceSecret = $"freshness-service-{Guid.NewGuid():N}";
        using var factory = await FactoryAsync(new Dictionary<string, string?>
        {
            [$"AuthorizationConsumers:{applicationCode}:CurrentSecret"] = serviceSecret
        });
        using var client = factory.CreateClient();
        var user = await CreateUserAsync(client);
        var applicationId = await CreateApplicationAsync(client, applicationCode);
        await CreateMembershipAsync(client, applicationId, user.Id);
        var firstRoleId = await CreateRoleAsync(client, applicationId, "dispatch-primary");
        var secondRoleId = await CreateRoleAsync(client, applicationId, "dispatch-secondary");
        var permissionId = await CreatePermissionAsync(client, applicationId, "dispatch.execute");
        await AssignAsync(client, $"/applications/{applicationId}/roles/{firstRoleId}/permissions/{permissionId}");
        await AssignAsync(client, $"/applications/{applicationId}/roles/{secondRoleId}/permissions/{permissionId}");
        await AssignAsync(client, $"/applications/{applicationId}/users/{user.Id}/roles/{firstRoleId}");
        await AssignAsync(client, $"/applications/{applicationId}/users/{user.Id}/roles/{secondRoleId}");
        var login = await LoginAsync(client, applicationCode, user.Email);

        await AssertConsumerPermissionAsync(client, login.AccessToken, applicationCode, serviceSecret, "dispatch.execute", true, 1);
        await AssertConsumerPermissionAsync(client, login.AccessToken, applicationCode, serviceSecret, "dispatch.denied", false, 1);

        await AssertOkAsync(client.PostAsync($"/applications/{applicationId}/users/{user.Id}/roles/{firstRoleId}/remove", null));
        await AssertConsumerPermissionAsync(client, login.AccessToken, applicationCode, serviceSecret, "dispatch.execute", true, 1);

        await AssertOkAsync(client.PostAsync($"/applications/{applicationId}/users/{user.Id}/roles/{secondRoleId}/remove", null));
        await AssertConsumerPermissionAsync(client, login.AccessToken, applicationCode, serviceSecret, "dispatch.execute", false, 0);

        await AssignAsync(client, $"/applications/{applicationId}/users/{user.Id}/roles/{firstRoleId}");
        await AssertOkAsync(client.PostAsync($"/applications/{applicationId}/roles/{firstRoleId}/permissions/{permissionId}/remove", null));
        await AssertConsumerPermissionAsync(client, login.AccessToken, applicationCode, serviceSecret, "dispatch.execute", false, 0);

        await AssignAsync(client, $"/applications/{applicationId}/roles/{firstRoleId}/permissions/{permissionId}");
        await AssertOkAsync(client.PostAsync($"/applications/{applicationId}/permissions/{permissionId}/deactivate", null));
        await AssertConsumerPermissionAsync(client, login.AccessToken, applicationCode, serviceSecret, "dispatch.execute", false, 0);

        await AssertOkAsync(client.PostAsync($"/applications/{applicationId}/permissions/{permissionId}/activate", null));
        await AssertOkAsync(client.PostAsync($"/applications/{applicationId}/roles/{firstRoleId}/deactivate", null));
        await AssertConsumerPermissionAsync(client, login.AccessToken, applicationCode, serviceSecret, "dispatch.execute", false, 0);
    }

    private static async Task<WebApplicationFactory<Program>> FactoryAsync(IReadOnlyDictionary<string, string?> values, TimeProvider? timeProvider = null)
    {
        foreach (var key in ManagedEnvironmentKeys)
        {
            Environment.SetEnvironmentVariable(key, values.GetValueOrDefault(key.Replace("__", ":")));
        }
        foreach (var value in values.Where(item => item.Key.StartsWith("AuthorizationConsumers:", StringComparison.Ordinal)))
        {
            Environment.SetEnvironmentVariable(value.Key.Replace(":", "__"), value.Value);
        }

        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            if (timeProvider is not null)
            {
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<TimeProvider>();
                    services.AddSingleton(timeProvider);
                });
            }
        });
        using var scope = factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AuthenticationDbContext>().Database.MigrateAsync();
        return factory;
    }

    private static async Task<(Guid Id, string Email)> CreateUserAsync(HttpClient client)
    {
        var email = $"consumer-{Guid.NewGuid():N}@example.test";
        using var response = await client.PostAsJsonAsync("/users", new { email, password = Password, firstName = "Consumer", lastName = "Test", displayName = "Consumer Test" });
        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
        return (JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetGuid(), email);
    }

    private static async Task<Guid> CreateApplicationAsync(HttpClient client, string code)
    {
        using var response = await client.PostAsJsonAsync("/applications", new { code, name = code });
        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetGuid();
    }

    private static async Task CreateMembershipAsync(HttpClient client, Guid applicationId, Guid userId)
    {
        using var response = await client.PostAsJsonAsync($"/applications/{applicationId}/memberships", new { userId });
        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
    }

    private static async Task<Guid> CreateRoleAsync(HttpClient client, Guid applicationId, string name)
    {
        using var response = await client.PostAsJsonAsync($"/applications/{applicationId}/roles", new { name });
        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreatePermissionAsync(HttpClient client, Guid applicationId, string code)
    {
        using var response = await client.PostAsJsonAsync($"/applications/{applicationId}/permissions", new { code });
        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetGuid();
    }

    private static async Task AssignAsync(HttpClient client, string path)
    {
        using var response = await client.PostAsync(path, null);
        Assert.IsTrue(response.StatusCode is HttpStatusCode.Created or HttpStatusCode.OK);
    }

    private static async Task<(Guid SessionId, string AccessToken)> LoginAsync(HttpClient client, string applicationCode, string email)
    {
        using var response = await client.PostAsJsonAsync("/auth/login", new { applicationCode, email, password = Password });
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        return (body.GetProperty("sessionId").GetGuid(), body.GetProperty("accessToken").GetString()!);
    }

    private static async Task<HttpResponseMessage> ResolveAsync(HttpClient client, string accessToken, string applicationCode, string serviceSecret)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/auth/authorization-context");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Add("X-GaussAuth-Application-Code", applicationCode);
        request.Headers.Add("X-GaussAuth-Consumer-Secret", serviceSecret);
        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> LogoutAsync(HttpClient client, string accessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/auth/logout");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await client.SendAsync(request);
    }

    private static async Task AssertUnauthorizedAsync(HttpResponseMessage response)
    {
        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.AreEqual("Bearer", response.Headers.WwwAuthenticate.Single().Scheme);
        var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.AreEqual("Access is not valid.", problem.GetProperty("title").GetString());
        Assert.AreEqual(401, problem.GetProperty("status").GetInt32());
    }

    private static async Task AssertConsumerPermissionAsync(HttpClient client, string accessToken, string applicationCode, string serviceSecret, string requiredPermission, bool expected, int expectedPermissionCount)
    {
        using var response = await ResolveAsync(client, accessToken, applicationCode, serviceSecret);
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var consumer = AuthorizationContextConsumer.FromJson(await response.Content.ReadAsStringAsync());
        Assert.AreEqual(expectedPermissionCount, consumer.PermissionCount);
        Assert.AreEqual(expected, consumer.Allows(requiredPermission));
    }

    private static async Task AssertOkAsync(Task<HttpResponseMessage> responseTask)
    {
        using var response = await responseTask;
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
    }

    private static readonly string[] ManagedEnvironmentKeys =
    ["RateLimiting__AuthorizationContext__PermitLimit", "RateLimiting__AuthorizationContext__WindowSeconds"];

    private static ConfiguredConsumerCredentialValidator CreateValidator(IReadOnlyDictionary<string, string?> values) =>
        new(new ConfigurationBuilder().AddInMemoryCollection(values).Build());
}
