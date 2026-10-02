using GaussAuth.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GaussAuth.Foundation.Tests;

[TestClass]
public sealed class CorrelationTests
{
    [TestMethod]
    public async Task Unexpected_failure_log_response_and_security_event_share_the_existing_trace_identifier()
    {
        var logs = new CapturingLoggerProvider();
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.AddLogging(logging => logging.AddProvider(logs));
            services.AddSingleton<IStartupFilter, TestFailureStartupFilter>();
        }));
        using var scope = factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AuthenticationDbContext>().Database.MigrateAsync();
        using var response = await factory.CreateClient().GetAsync("/forced-failure");
        Assert.AreEqual(System.Net.HttpStatusCode.InternalServerError, response.StatusCode);
        var traceId = response.Headers.GetValues("X-Correlation-Id").Single();
        var db = scope.ServiceProvider.GetRequiredService<AuthenticationDbContext>();
        var securityEvent = await db.SecurityEvents.OrderByDescending(item => item.OccurredAtUtc)
            .FirstAsync(item => item.EventType == "application.failure.unexpected");
        Assert.AreEqual(traceId, securityEvent.CorrelationId);
        Assert.IsTrue(logs.Entries.Any(entry => entry.Contains(traceId, StringComparison.Ordinal)));
    }
}
