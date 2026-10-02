using System.Net;
using System.Security.Cryptography;
using GaussAuth.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GaussAuth.Foundation.Tests;

[TestClass]
[DoNotParallelize]
public sealed class ReadinessTests
{
    private static readonly string SigningKey = ECDsa.Create(ECCurve.NamedCurves.nistP256).ExportPkcs8PrivateKeyPem();

    [TestMethod]
    public async Task Ready_dependencies_return_empty_204_and_liveness_does_not_touch_the_database()
    {
        using var environment = new ReadinessEnvironment();
        await using var database = await ThrowawayDatabase.CreateAsync();
        await MigrationServices.MigrateAsync(database.ConnectionString);
        using var factory = CreateFactory(database.ConnectionString, environment.Root);
        using var client = factory.CreateClient();

        using var ready = await client.GetAsync("/health/ready");
        using var live = await client.GetAsync("/health/live");

        Assert.AreEqual(HttpStatusCode.NoContent, ready.StatusCode);
        Assert.AreEqual(string.Empty, await ready.Content.ReadAsStringAsync());
        Assert.AreEqual(HttpStatusCode.NoContent, live.StatusCode);
        Assert.AreEqual(string.Empty, await live.Content.ReadAsStringAsync());
        Assert.IsFalse(ready.Headers.Any(header => header.Key.Contains("database", StringComparison.OrdinalIgnoreCase) || header.Key.Contains("version", StringComparison.OrdinalIgnoreCase) || header.Key.Contains("configuration", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public async Task Unreachable_database_returns_503_while_liveness_stays_204()
    {
        using var environment = new ReadinessEnvironment();
        using var factory = CreateFactory("Host=127.0.0.1;Port=1;Database=unreachable;Username=test;Password=not_a_secret", environment.Root);
        using var client = factory.CreateClient();

        using var ready = await client.GetAsync("/health/ready");
        using var live = await client.GetAsync("/health/live");

        Assert.AreEqual(HttpStatusCode.ServiceUnavailable, ready.StatusCode);
        Assert.AreEqual(string.Empty, await ready.Content.ReadAsStringAsync());
        Assert.AreEqual(HttpStatusCode.NoContent, live.StatusCode);
    }

    [TestMethod]
    public async Task Pending_migrations_return_503_then_recover_within_ten_seconds()
    {
        using var environment = new ReadinessEnvironment();
        await using var database = await ThrowawayDatabase.CreateAsync();
        using var factory = CreateFactory(database.ConnectionString, environment.Root);
        using var client = factory.CreateClient();

        using (var beforeMigration = await client.GetAsync("/health/ready"))
        {
            Assert.AreEqual(HttpStatusCode.ServiceUnavailable, beforeMigration.StatusCode);
            Assert.AreEqual(string.Empty, await beforeMigration.Content.ReadAsStringAsync());
        }

        await MigrationServices.MigrateAsync(database.ConnectionString);
        await Task.Delay(TimeSpan.FromSeconds(6));

        using var afterMigration = await client.GetAsync("/health/ready");
        Assert.AreEqual(HttpStatusCode.NoContent, afterMigration.StatusCode);
        Assert.AreEqual(string.Empty, await afterMigration.Content.ReadAsStringAsync());
    }

    [TestMethod]
    public async Task Unusable_profile_storage_returns_503()
    {
        using var environment = new ReadinessEnvironment();
        await using var database = await ThrowawayDatabase.CreateAsync();
        await MigrationServices.MigrateAsync(database.ConnectionString);
        var fileRoot = Path.Combine(environment.Root, "not-a-directory");
        await File.WriteAllTextAsync(fileRoot, "not a directory");
        using var factory = CreateFactory(database.ConnectionString, fileRoot);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/health/ready");

        Assert.AreEqual(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.AreEqual(string.Empty, await response.Content.ReadAsStringAsync());
    }

    private static WebApplicationFactory<Program> CreateFactory(string connection, string storageRoot)
    {
        Environment.SetEnvironmentVariable("ConnectionStrings__AuthenticationDatabase", connection);
        Environment.SetEnvironmentVariable("Sessions__Signing__PrivateKeyPem", SigningKey);
        Environment.SetEnvironmentVariable("ProfileImages__RootPath", storageRoot);
        Environment.SetEnvironmentVariable("ProfileImages__StorageIsPersistent", "true");
        Environment.SetEnvironmentVariable("DataProtection__KeysPath", Path.Combine(Path.GetTempPath(), "gaussauth-readiness-keys"));
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.Sources.Clear();
                configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:AuthenticationDatabase"] = connection });
            });
        });
    }

    private sealed class ReadinessEnvironment : IDisposable
    {
        private readonly Dictionary<string, string?> previous = new()
        {
            ["ConnectionStrings__AuthenticationDatabase"] = Environment.GetEnvironmentVariable("ConnectionStrings__AuthenticationDatabase"),
            ["Sessions__Signing__PrivateKeyPem"] = Environment.GetEnvironmentVariable("Sessions__Signing__PrivateKeyPem"),
            ["ProfileImages__RootPath"] = Environment.GetEnvironmentVariable("ProfileImages__RootPath"),
            ["ProfileImages__StorageIsPersistent"] = Environment.GetEnvironmentVariable("ProfileImages__StorageIsPersistent"),
            ["DataProtection__KeysPath"] = Environment.GetEnvironmentVariable("DataProtection__KeysPath")
        };

        public ReadinessEnvironment()
        {
            Root = Path.Combine(Path.GetTempPath(), "gaussauth-readiness-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
        }

        public string Root { get; }

        public void Dispose()
        {
            foreach (var pair in previous) Environment.SetEnvironmentVariable(pair.Key, pair.Value);
            if (Directory.Exists(Root)) Directory.Delete(Root, recursive: true);
        }
    }
}
