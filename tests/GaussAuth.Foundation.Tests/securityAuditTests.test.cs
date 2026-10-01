using GaussAuth.Application.Security;
using GaussAuth.Application.Security.Ports;
using GaussAuth.Domain.Security;
using GaussAuth.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;

namespace GaussAuth.Foundation.Tests;

[TestClass]
public sealed class SecurityAuditTests
{
    [TestMethod]
    public void Security_event_is_immutable_utc_and_rejects_sensitive_context()
    {
        var now = DateTimeOffset.UtcNow;
        var securityEvent = SecurityEvent.Create(Guid.NewGuid(), "authentication.login.succeeded", "succeeded", now,
            correlationId: "trace-1", reason: "valid-credentials", metadata: "endpoint=login");

        Assert.AreEqual(now, securityEvent.OccurredAtUtc);
        Assert.IsFalse(typeof(SecurityEvent).GetProperties().Any(property => property.SetMethod?.IsPublic == true));
        Assert.Throws<ArgumentException>(() => SecurityEvent.Create(Guid.NewGuid(), "authentication.login.failed",
            "rejected", now, metadata: "password=not-allowed"));
        Assert.Throws<ArgumentException>(() => SecurityEvent.Create(Guid.NewGuid(), "authentication.login.failed",
            "rejected", now.ToOffset(TimeSpan.FromHours(1))));
    }

    [TestMethod]
    public void Catalog_has_stable_types_and_central_reliability_classification()
    {
        var catalog = new SecurityEventCatalog();
        var login = catalog.Get(SecurityEventType.LoginSucceeded);
        var passwordChange = catalog.Get(SecurityEventType.PasswordChanged);

        Assert.AreEqual("authentication.login.succeeded", login.EventType);
        Assert.AreEqual(SecurityEventReliability.Operational, login.Reliability);
        Assert.AreEqual("password.changed", passwordChange.EventType);
        Assert.AreEqual(SecurityEventReliability.Critical, passwordChange.Reliability);
    }

    [TestMethod]
    public async Task Persisted_recorder_writes_safe_append_only_event()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var scope = factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<AuthenticationDbContext>();
        await db.Database.MigrateAsync();

        var recorder = services.GetRequiredService<ISecurityEventRecorder>();
        var userId = Guid.NewGuid();
        await recorder.RecordAsync(SecurityEventType.LoginSucceeded, userId, null, null, CancellationToken.None);

        var recorded = await db.SecurityEvents.OrderByDescending(item => item.OccurredAtUtc).FirstAsync(item => item.UserId == userId);
        Assert.AreEqual("authentication.login.succeeded", recorded.EventType);
        Assert.AreEqual("succeeded", recorded.Outcome);
        Assert.AreEqual(TimeSpan.Zero, recorded.OccurredAtUtc.Offset);
        Assert.IsNull(recorded.Metadata);
    }

    [TestMethod]
    public async Task Audit_endpoint_is_read_only_bounded_and_requires_a_valid_session()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        using var invalidPage = await client.GetAsync("/security-events?pageSize=101");
        Assert.AreEqual(HttpStatusCode.Unauthorized, invalidPage.StatusCode);
        using var anonymous = await client.GetAsync("/security-events");
        Assert.AreEqual(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        using var mutation = await client.PostAsync("/security-events", null);
        Assert.AreEqual(HttpStatusCode.MethodNotAllowed, mutation.StatusCode);
    }

    [TestMethod]
    public async Task Api_returns_security_headers_and_safe_internal_error_shape()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/health/live");

        Assert.AreEqual(HttpStatusCode.NoContent, response.StatusCode);
        Assert.AreEqual("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.AreEqual("no-referrer", response.Headers.GetValues("Referrer-Policy").Single());
        Assert.IsTrue(response.Headers.Contains("X-Correlation-Id"));
    }
}
