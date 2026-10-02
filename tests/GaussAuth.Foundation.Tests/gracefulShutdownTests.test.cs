using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;

namespace GaussAuth.Foundation.Tests;

[TestClass]
public sealed class GracefulShutdownTests
{
    [TestMethod]
    public void Web_host_has_the_documented_thirty_second_shutdown_timeout()
    {
        using var factory = new WebApplicationFactory<Program>();
        var options = factory.Services.GetRequiredService<IOptions<HostOptions>>().Value;
        Assert.AreEqual(TimeSpan.FromSeconds(30), options.ShutdownTimeout);
    }
}
