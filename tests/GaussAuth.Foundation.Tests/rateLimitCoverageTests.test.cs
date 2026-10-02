using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace GaussAuth.Foundation.Tests;

[TestClass]
public sealed class RateLimitCoverageTests
{
    [TestMethod]
    public void Health_is_the_only_explicitly_exempt_probe_pair()
    {
        using var factory = new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program>();
        var endpoints = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>().ToArray();
        foreach (var path in new[] { "/health/live", "/health/ready" })
            Assert.IsNotNull(endpoints.Single(endpoint => endpoint.RoutePattern.RawText == path));
        foreach (var path in new[] { "/auth/login", "/auth/password/recovery", "/auth/password/reset", "/auth/authorization-context" })
            Assert.IsTrue(endpoints.Single(endpoint => endpoint.RoutePattern.RawText == path).Metadata.GetMetadata<EnableRateLimitingAttribute>() is not null, path);
    }
}
