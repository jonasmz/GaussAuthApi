using System.Net;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GaussAuth.Foundation.Tests;

[TestClass]
public sealed class StartupTests
{
    private const string TestConnection = "Host=localhost;Database=foundation_test;Username=test;Password=not_a_secret";
    private static readonly string TestProfileImagesRoot = Path.Combine(Path.GetTempPath(), "gaussauth-startup-test-images");

    private static readonly string TestSigningKeyPem = ECDsa.Create(ECCurve.NamedCurves.nistP256).ExportPkcs8PrivateKeyPem();

    [TestMethod]
    public async Task Liveness_returns_empty_204_without_database_access()
    {
        using var factory = CreateFactory(TestConnection);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/health/live");

        Assert.AreEqual(HttpStatusCode.NoContent, response.StatusCode);
        Assert.AreEqual(string.Empty, await response.Content.ReadAsStringAsync());
    }

    [TestMethod]
    public async Task Unknown_subcommand_is_rejected_instead_of_starting_the_web_host()
    {
        var result = await ApiProcess.RunAsync(
            new Dictionary<string, string?> { ["ASPNETCORE_ENVIRONMENT"] = "Production" },
            TimeSpan.FromSeconds(15),
            "migrat");

        Assert.IsFalse(result.TimedOut, "A mistyped subcommand must exit, not start the server.");
        Assert.AreEqual(64, result.ExitCode);
        Assert.Contains("Unknown command 'migrat'", result.Output);
    }

    [DataRow("")]
    [DataRow("not a connection string")]
    [TestMethod]
    public async Task Invalid_database_configuration_fails_without_echoing_its_value(string connection)
    {
        var result = await ApiProcess.RunAsync(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings__AuthenticationDatabase"] = connection,
                ["ASPNETCORE_ENVIRONMENT"] = "Production",
                ["ProfileImages__RootPath"] = TestProfileImagesRoot
            },
            TimeSpan.FromSeconds(10));
        if (result.TimedOut) Assert.Fail("API remained running with invalid database configuration.");
        var output = result.Output;

        Assert.AreNotEqual(0, result.ExitCode);
        Assert.Contains("database connection configuration", output);
        if (connection.Length > 0)
        {
            Assert.DoesNotContain(connection, output);
        }
    }

    [DataRow("Sessions__SessionLifetimeMinutes", "0", true)]
    [DataRow("Sessions__SessionLifetimeMinutes", "-5", true)]
    [DataRow("Sessions__AccessTokenLifetimeMinutes", "0", true)]
    [DataRow("Sessions__AccessTokenLifetimeMinutes", "-1", true)]
    [DataRow("Sessions__AccessTokenLifetimeMinutes", "999", true)]
    [DataRow("Sessions__Signing__PrivateKeyPem", "", false)]
    [TestMethod]
    public async Task Invalid_session_configuration_fails_generically_without_echoing_values(string key, string value, bool withKey)
    {
        var result = await ApiProcess.RunAsync(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings__AuthenticationDatabase"] = TestConnection,
                ["ASPNETCORE_ENVIRONMENT"] = "Production",
                ["ProfileImages__RootPath"] = TestProfileImagesRoot,
                ["Sessions__Signing__PrivateKeyPem"] = withKey ? TestSigningKeyPem : "",
                [key] = value
            },
            TimeSpan.FromSeconds(15));
        if (result.TimedOut) Assert.Fail("API remained running with invalid session configuration.");
        var output = result.Output;
        Assert.AreNotEqual(0, result.ExitCode);
        Assert.IsTrue(output.Contains("Session lifetime configuration is missing or invalid.") ||
                      output.Contains("Access credential signing configuration is missing or invalid."));
        Assert.DoesNotContain("BEGIN PRIVATE KEY", output);
        Assert.DoesNotContain("not_a_secret", output);
    }

    [TestMethod]
    public async Task Development_without_a_signing_key_starts_with_an_ephemeral_key()
    {
        Environment.SetEnvironmentVariable("Sessions__Signing__PrivateKeyPem", "");
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.Sources.Clear();
                configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:AuthenticationDatabase"] = TestConnection });
            });
        });
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/health/live");

        Assert.AreEqual(HttpStatusCode.NoContent, response.StatusCode);
    }

    [TestMethod]
    public async Task Unexpected_production_error_returns_generic_problem_details()
    {
        using var factory = CreateFactory(TestConnection, injectFailure: true);
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        request.Headers.Accept.ParseAdd("application/problem+json");

        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.AreEqual(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.StartsWith("application/problem+json", response.Content.Headers.ContentType?.ToString());
        Assert.DoesNotContain("SELECT sensitive", body);
        Assert.DoesNotContain("/private/server/path", body);
        Assert.DoesNotContain("not_a_secret", body);
        Assert.DoesNotContain("System.InvalidOperationException", body);
    }

    [TestMethod]
    public async Task Unexpected_error_has_safe_fallback_for_non_json_accept()
    {
        using var factory = CreateFactory(TestConnection, injectFailure: true);
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        request.Headers.Accept.ParseAdd("text/plain");

        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.AreEqual(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.AreEqual("An unexpected error occurred.", body);
    }

    private static WebApplicationFactory<Program> CreateFactory(string connection, bool injectFailure = false)
    {
        // Environment variables are visible to the eager configuration reads in Program.cs; in-memory sources are not.
        Environment.SetEnvironmentVariable("Sessions__Signing__PrivateKeyPem", TestSigningKeyPem);
        Environment.SetEnvironmentVariable("ProfileImages__RootPath", TestProfileImagesRoot);
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.Sources.Clear();
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:AuthenticationDatabase"] = connection
                });
            });

            if (injectFailure)
            {
                builder.ConfigureTestServices(services =>
                    services.AddTransient<IStartupFilter, TestFailureStartupFilter>());
            }
        });
    }

}
