using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GaussAuth.Foundation.Tests;

[TestClass]
public sealed class SensitiveLoggingTests
{
    [TestMethod]
    public async Task Forced_failure_does_not_expose_exception_payload_or_authorization_header_in_body_or_logs()
    {
        var logs = new CapturingLoggerProvider();
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.AddLogging(logging => logging.AddProvider(logs));
            services.AddSingleton<IStartupFilter, TestFailureStartupFilter>();
        }));
        using var request = new HttpRequestMessage(HttpMethod.Get, "/forced-failure");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "access-token-value");
        using var response = await factory.CreateClient().SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        var combined = body + "\n" + string.Join("\n", logs.Entries);
        foreach (var forbidden in new[] { "access-token-value", "SELECT sensitive", "/private/server/path", "not_a_secret", "Authorization" })
            Assert.DoesNotContain(forbidden, combined, forbidden);
    }

    [TestMethod]
    public void Logging_configuration_uses_safe_production_defaults_without_required_secret_values()
    {
        var root = FindRoot();
        var api = Path.Combine(root, "src", "GaussAuth.Api");
        var production = File.ReadAllText(Path.Combine(api, "appsettings.Production.json"));
        var common = File.ReadAllText(Path.Combine(api, "appsettings.json"));
        Assert.Contains("\"FormatterName\": \"json\"", production);
        Assert.Contains("\"IncludeScopes\": true", production);
        Assert.Contains("\"Default\": \"Information\"", common);
        Assert.DoesNotContain("PrivateKey", common + production);
        Assert.DoesNotContain("AuthenticationDatabase", common + production);
    }

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory.Parent is not null && !File.Exists(Path.Combine(directory.FullName, "GaussAuth.slnx"))) directory = directory.Parent;
        return directory.FullName;
    }
}
