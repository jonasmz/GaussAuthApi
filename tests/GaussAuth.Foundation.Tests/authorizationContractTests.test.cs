using GaussAuth.Application.AuthorizationContext.Ports;
using GaussAuth.Application.Security;
using GaussAuth.Infrastructure.AuthorizationContext;
using Microsoft.Extensions.Configuration;

namespace GaussAuth.Foundation.Tests;

[TestClass]
public sealed class AuthorizationContractTests
{
    [TestMethod]
    public async Task Consumer_credential_is_bound_to_its_configured_application_and_allows_rotation()
    {
        var validator = CreateValidator(new Dictionary<string, string?>
        {
            ["AuthorizationConsumers:orders:CurrentSecret"] = "orders-current-secret",
            ["AuthorizationConsumers:orders:RetiringSecret"] = "orders-retiring-secret",
            ["AuthorizationConsumers:billing:CurrentSecret"] = "billing-current-secret"
        });

        Assert.IsTrue((await validator.ValidateAsync("orders", "orders-current-secret", CancellationToken.None)).IsValid);
        Assert.IsTrue((await validator.ValidateAsync("orders", "orders-retiring-secret", CancellationToken.None)).IsValid);
        Assert.IsFalse((await validator.ValidateAsync("billing", "orders-current-secret", CancellationToken.None)).IsValid);
        Assert.IsFalse((await validator.ValidateAsync("orders", "billing-current-secret", CancellationToken.None)).IsValid);
    }

    [TestMethod]
    public void Consumer_configuration_rejects_secret_reuse_without_exposing_the_secret()
    {
        var exception = Assert.ThrowsExactly<InvalidOperationException>(() => CreateValidator(new Dictionary<string, string?>
        {
            ["AuthorizationConsumers:orders:CurrentSecret"] = "shared-secret",
            ["AuthorizationConsumers:billing:CurrentSecret"] = "shared-secret"
        }));

        Assert.IsFalse(exception.Message.Contains("shared-secret", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Authorization_context_events_are_safe_categorical_values()
    {
        Assert.IsTrue(Enum.IsDefined(SecurityEventType.AuthorizationContextResolved));
        Assert.IsTrue(Enum.IsDefined(SecurityEventType.AuthorizationContextRejected));
    }

    private static ConfiguredConsumerCredentialValidator CreateValidator(IReadOnlyDictionary<string, string?> values) =>
        new(new ConfigurationBuilder().AddInMemoryCollection(values).Build());
}
