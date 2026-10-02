using GaussAuth.Api.DependencyInjection;
using GaussAuth.Infrastructure.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Builder;

namespace GaussAuth.Foundation.Tests;

[TestClass]
public sealed class ForwardedHeadersTests
{
    [DataRow("0.0.0.0/0")]
    [DataRow("::/0")]
    [DataRow("not-a-network")]
    [TestMethod]
    public void Unsafe_or_invalid_trusted_network_is_refused(string network)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["ForwardedHeaders:KnownNetworks:0"] = network });
        using var app = builder.Build();
        Assert.Throws<StartupConfigurationException>(() => app.UseConfiguredForwardedHeaders());
    }
}
