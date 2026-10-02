using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace GaussAuth.Foundation.Tests;

[TestClass]
public sealed class HttpBaselineTests
{
    [TestMethod]
    public async Task Liveness_has_security_headers_no_server_header_and_no_cors()
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.UseEnvironment("Development"));
        using var response = await factory.CreateClient().GetAsync("/health/live");
        Assert.AreEqual(HttpStatusCode.NoContent, response.StatusCode);
        Assert.IsTrue(response.Headers.Contains("X-Content-Type-Options"));
        Assert.IsFalse(response.Headers.Contains("Server"));
        Assert.IsFalse(response.Headers.Any(header => header.Key.StartsWith("Access-Control-", StringComparison.OrdinalIgnoreCase)));
    }
}
