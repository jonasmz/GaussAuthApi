using System.Net;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using GaussAuth.Application.Passwords.Ports;
using GaussAuth.Infrastructure.Identity;
using GaussAuth.Infrastructure.Passwords;
using GaussAuth.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GaussAuth.Foundation.Tests;

/// <summary>
/// Password recovery in Production-class environments without a delivery adapter (FR-018a): the response stays generic and
/// identical for existing and unknown accounts, and no reset credential is generated, logged, audited or returned.
/// </summary>
[TestClass]
public sealed class RecoveryWithoutDeliveryTests
{
    private static readonly string[] Overridden =
    [
        "Sessions__Signing__PrivateKeyPem", "ProfileImages__RootPath", "ProfileImages__StorageIsPersistent",
        "DataProtection__KeysPath", "PasswordRecovery__DeliveryFile", "RateLimiting__PasswordRecovery__PermitLimit"
    ];

    [TestMethod]
    public async Task Recovery_without_a_delivery_channel_is_generic_and_generates_no_credential()
    {
        var saved = Overridden.ToDictionary(key => key, Environment.GetEnvironmentVariable);
        var root = Path.Combine(Path.GetTempPath(), $"gaussauth-recovery-{Guid.NewGuid():N}");
        var deliveryFile = Path.Combine(root, "delivery.jsonl");
        try
        {
            Environment.SetEnvironmentVariable("Sessions__Signing__PrivateKeyPem", ECDsa.Create(ECCurve.NamedCurves.nistP256).ExportPkcs8PrivateKeyPem());
            Environment.SetEnvironmentVariable("ProfileImages__RootPath", Path.Combine(root, "images"));
            Environment.SetEnvironmentVariable("ProfileImages__StorageIsPersistent", "true");
            Environment.SetEnvironmentVariable("DataProtection__KeysPath", Path.Combine(root, "keys"));
            // A configured file must not turn the development adapter into a Production channel.
            Environment.SetEnvironmentVariable("PasswordRecovery__DeliveryFile", deliveryFile);
            Environment.SetEnvironmentVariable("RateLimiting__PasswordRecovery__PermitLimit", "1000");

            var generated = new StrongBox<int>();
            var logs = new CapturingLoggerProvider();
            using var factory = new WebApplicationFactory<Program>().WithGlobalAdministrators().WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Staging");
                builder.ConfigureLogging(logging => logging.AddProvider(logs));
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<IPasswordCredentialService>();
                    services.AddScoped<IdentityPasswordCredentialService>();
                    services.AddScoped<IPasswordCredentialService>(provider =>
                        new CountingPasswordCredentialService(provider.GetRequiredService<IdentityPasswordCredentialService>(), generated));
                });
            });
            using (var scope = factory.Services.CreateScope())
            {
                await scope.ServiceProvider.GetRequiredService<AuthenticationDbContext>().Database.MigrateAsync();
                Assert.IsFalse(scope.ServiceProvider.GetRequiredService<IRecoveryDelivery>().IsAvailable);
            }

            var email = $"recovery-{Guid.NewGuid():N}@example.test";
            var userId = await factory.CreateUserAsync(email);
            using var client = factory.CreateClient();

            using var existing = await client.PostAsJsonAsync("/auth/password/recovery", new { email });
            using var unknown = await client.PostAsJsonAsync("/auth/password/recovery", new { email = $"unknown-{Guid.NewGuid():N}@example.test" });
            var existingBody = await existing.Content.ReadAsStringAsync();
            var unknownBody = await unknown.Content.ReadAsStringAsync();

            Assert.AreEqual(HttpStatusCode.Accepted, existing.StatusCode);
            Assert.AreEqual(existing.StatusCode, unknown.StatusCode);
            Assert.AreEqual(existingBody, unknownBody);
            Assert.AreEqual(0, generated.Value, "No reset credential may be generated without a delivery channel.");
            Assert.IsFalse(File.Exists(deliveryFile), "The development file adapter must not be used in a Production-class environment.");

            using (var scope = factory.Services.CreateScope())
            {
                var events = await scope.ServiceProvider.GetRequiredService<AuthenticationDbContext>().SecurityEvents
                    .Where(item => item.UserId == userId && item.EventType.Contains("Recovery")).CountAsync();
                Assert.AreEqual(0, events, "No recovery security event may be recorded when nothing was issued.");
            }

            Assert.IsFalse(logs.Entries.Any(entry => entry.Contains(email, StringComparison.OrdinalIgnoreCase) && entry.Contains("recovery", StringComparison.OrdinalIgnoreCase)),
                "The requested address must not be logged for a recovery that issued nothing.");
        }
        finally
        {
            foreach (var (key, value) in saved) Environment.SetEnvironmentVariable(key, value);
        }
    }

    [TestMethod]
    public async Task Control_recovery_with_a_delivery_channel_still_generates_and_delivers_a_credential()
    {
        var saved = Environment.GetEnvironmentVariable("PasswordRecovery__DeliveryFile");
        var deliveryFile = Path.Combine(Path.GetTempPath(), $"gaussauth-recovery-{Guid.NewGuid():N}", "delivery.jsonl");
        try
        {
            Environment.SetEnvironmentVariable("PasswordRecovery__DeliveryFile", deliveryFile);
            var generated = new StrongBox<int>();
            using var factory = new WebApplicationFactory<Program>().WithGlobalAdministrators().WithWebHostBuilder(builder =>
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<IPasswordCredentialService>();
                    services.AddScoped<IdentityPasswordCredentialService>();
                    services.AddScoped<IPasswordCredentialService>(provider =>
                        new CountingPasswordCredentialService(provider.GetRequiredService<IdentityPasswordCredentialService>(), generated));
                }));
            using (var scope = factory.Services.CreateScope())
            {
                await scope.ServiceProvider.GetRequiredService<AuthenticationDbContext>().Database.MigrateAsync();
                Assert.IsTrue(scope.ServiceProvider.GetRequiredService<IRecoveryDelivery>().IsAvailable);
            }

            var email = $"recovery-{Guid.NewGuid():N}@example.test";
            await factory.CreateUserAsync(email);
            using var client = factory.CreateClient();
            using var response = await client.PostAsJsonAsync("/auth/password/recovery", new { email });

            Assert.AreEqual(HttpStatusCode.Accepted, response.StatusCode);
            Assert.AreEqual(1, generated.Value);
            Assert.IsTrue(File.Exists(deliveryFile));
        }
        finally
        {
            Environment.SetEnvironmentVariable("PasswordRecovery__DeliveryFile", saved);
        }
    }

    [TestMethod]
    public void The_development_file_adapter_is_unavailable_in_production_class_environments_even_when_configured()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["PasswordRecovery:DeliveryFile"] = "/tmp/x.jsonl" }).Build();

        Assert.IsFalse(new ProtectedFileRecoveryDelivery(configuration, new TestHostEnvironment("Production")).IsAvailable);
        Assert.IsFalse(new ProtectedFileRecoveryDelivery(configuration, new TestHostEnvironment("Staging")).IsAvailable);
        Assert.IsTrue(new ProtectedFileRecoveryDelivery(configuration, new TestHostEnvironment("Development")).IsAvailable);
        Assert.IsTrue(new ProtectedFileRecoveryDelivery(configuration, new TestHostEnvironment("Testing")).IsAvailable);

        var unconfigured = new ConfigurationBuilder().Build();
        Assert.IsFalse(new ProtectedFileRecoveryDelivery(unconfigured, new TestHostEnvironment("Development")).IsAvailable);
    }
}
