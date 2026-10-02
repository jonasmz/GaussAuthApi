using System.Security.Cryptography;
using GaussAuth.Api.Startup;

namespace GaussAuth.Foundation.Tests;

/// <summary>
/// Startup behavior of the real API process in Production-class environments: critical configuration that is missing or
/// invalid refuses the start, names only the setting (never a value), prints no stack trace, and development
/// conveniences stay out of Production-class environments (FR-014–019, FR-041, FR-041a, SC-004, SC-005).
/// </summary>
[TestClass]
public sealed class ProductionStartupValidationTests
{
    private const string Sentinel = "SENTINEL_9f3a";
    private static readonly TimeSpan RefusalTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan StartTimeout = TimeSpan.FromSeconds(60);

    [DataRow("Production", "ConnectionStrings__AuthenticationDatabase", null, "ConnectionStrings:AuthenticationDatabase")]
    [DataRow("Production", "ConnectionStrings__AuthenticationDatabase", "not a connection string " + Sentinel, "ConnectionStrings:AuthenticationDatabase")]
    [DataRow("Production", "ConnectionStrings__AuthenticationDatabase", "Host=localhost", "ConnectionStrings:AuthenticationDatabase")]
    [DataRow("Production", "Sessions__Signing__PrivateKeyPem", null, "Sessions:Signing:PrivateKeyPem")]
    [DataRow("Staging", "Sessions__Signing__PrivateKeyPem", null, "Sessions:Signing:PrivateKeyPem")]
    [DataRow("Production", "Sessions__Signing__PrivateKeyPem", "-----BEGIN PRIVATE KEY-----\n" + Sentinel + "\n-----END PRIVATE KEY-----", "Sessions:Signing:PrivateKeyPem")]
    [DataRow("Production", "ProfileImages__RootPath", null, "ProfileImages:RootPath")]
    [DataRow("Production", "ProfileImages__RootPath", "relative/" + Sentinel, "ProfileImages:RootPath")]
    [DataRow("Production", "ProfileImages__StorageIsPersistent", null, "ProfileImages:StorageIsPersistent")]
    [DataRow("Staging", "ProfileImages__StorageIsPersistent", null, "ProfileImages:StorageIsPersistent")]
    [DataRow("Production", "ProfileImages__StorageIsPersistent", Sentinel, "ProfileImages:StorageIsPersistent")]
    [DataRow("Production", "DataProtection__KeysPath", null, "DataProtection:KeysPath")]
    [DataRow("Staging", "DataProtection__KeysPath", null, "DataProtection:KeysPath")]
    [DataRow("Production", "DataProtection__KeysPath", "relative/" + Sentinel, "DataProtection:KeysPath")]
    [DataRow("Production", "Sessions__SessionLifetimeMinutes", "0", "Sessions:SessionLifetimeMinutes")]
    [DataRow("Production", "Sessions__SessionLifetimeMinutes", "-5", "Sessions:SessionLifetimeMinutes")]
    [DataRow("Production", "Sessions__AccessTokenLifetimeMinutes", "0", "Sessions:AccessTokenLifetimeMinutes")]
    [DataRow("Production", "Sessions__AccessTokenLifetimeMinutes", "999", "Sessions:AccessTokenLifetimeMinutes")]
    [DataRow("Production", "Sessions__AccessTokenLifetimeMinutes", Sentinel, "Sessions:AccessTokenLifetimeMinutes")]
    [DataRow("Production", "Identity__Lockout__MaxFailedAccessAttempts", "0", "Identity:Lockout:MaxFailedAccessAttempts")]
    [DataRow("Production", "RequestLimits__MaxBodyBytes", "0", "RequestLimits:MaxBodyBytes")]
    [DataRow("Production", "RateLimiting__Login__PermitLimit", "0", "RateLimiting:Login:PermitLimit")]
    [DataRow("Production", "RateLimiting__Administration__WindowSeconds", "-1", "RateLimiting:Administration:WindowSeconds")]
    [DataRow("Production", "RateLimiting__PasswordReset__PermitLimit", Sentinel, "RateLimiting:PasswordReset:PermitLimit")]
    [DataRow("Production", "Administration__GlobalAdministratorUserIds__0", Sentinel, "Administration:GlobalAdministratorUserIds")]
    [DataRow("Production", "SecurityAudit__GlobalReviewerUserId", "6f1d6f0c-5a36-4a9c-9d1c-0d2a6d2f5a11", "SecurityAudit:GlobalReviewerUserId")]
    [DataRow("Production", "SecurityAudit__RetentionDays", "0", "SecurityAudit:RetentionDays")]
    [TestMethod]
    public async Task Invalid_or_missing_critical_configuration_refuses_startup_naming_only_the_setting(
        string environmentName, string variable, string? value, string expectedSetting)
    {
        var environment = ValidEnvironment(environmentName);
        environment[variable] = value;

        var result = await ApiProcess.RunAsync(environment, RefusalTimeout);

        AssertRefused(result, expectedSetting);
    }

    [TestMethod]
    public async Task A_signing_key_on_the_wrong_curve_is_refused_without_echoing_key_material()
    {
        using var wrongCurve = ECDsa.Create(ECCurve.NamedCurves.nistP384);
        var environment = ValidEnvironment("Production");
        environment["Sessions__Signing__PrivateKeyPem"] = wrongCurve.ExportPkcs8PrivateKeyPem();

        var result = await ApiProcess.RunAsync(environment, RefusalTimeout);

        AssertRefused(result, "Sessions:Signing:PrivateKeyPem");
    }

    [TestMethod]
    public async Task A_public_only_signing_key_is_refused()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var environment = ValidEnvironment("Production");
        environment["Sessions__Signing__PrivateKeyPem"] = key.ExportSubjectPublicKeyInfoPem();

        var result = await ApiProcess.RunAsync(environment, RefusalTimeout);

        AssertRefused(result, "Sessions:Signing:PrivateKeyPem");
    }

    [TestMethod]
    public async Task An_unreadable_signing_key_file_is_refused_naming_the_file_setting()
    {
        var environment = ValidEnvironment("Production");
        environment["Sessions__Signing__PrivateKeyPem"] = null;
        environment["Sessions__Signing__PrivateKeyPemFile"] = Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.pem");

        var result = await ApiProcess.RunAsync(environment, RefusalTimeout);

        AssertRefused(result, "Sessions:Signing:PrivateKeyPemFile");
    }

    [TestMethod]
    public async Task A_key_ring_location_that_cannot_be_created_is_refused()
    {
        var blockingFile = Path.Combine(Path.GetTempPath(), $"gaussauth-not-a-directory-{Guid.NewGuid():N}");
        await File.WriteAllTextAsync(blockingFile, "x");
        try
        {
            var environment = ValidEnvironment("Production");
            environment["DataProtection__KeysPath"] = Path.Combine(blockingFile, "keys");

            var result = await ApiProcess.RunAsync(environment, RefusalTimeout);

            AssertRefused(result, "DataProtection:KeysPath");
        }
        finally
        {
            File.Delete(blockingFile);
        }
    }

    [DataRow("Production")]
    [DataRow("Staging")]
    [TestMethod]
    public async Task Complete_valid_configuration_starts_in_production_class_environments(string environmentName)
    {
        var result = await ApiProcess.RunAsync(ValidEnvironment(environmentName), StartTimeout, output => output.Contains("Application started"));

        Assert.IsFalse(result.TimedOut, $"The API did not start. Output:{Environment.NewLine}{result.Output}");
        Assert.AreEqual(-2, result.ExitCode, "The API exited instead of staying up.");
        Assert.DoesNotContain("Startup refused", result.Output);
        Assert.DoesNotContain("BEGIN PRIVATE KEY", result.Output);
        Assert.DoesNotContain("not_a_secret", result.Output);
    }

    [TestMethod]
    public async Task Production_declared_non_persistent_storage_starts_with_a_visible_warning()
    {
        var environment = ValidEnvironment("Production");
        environment["ProfileImages__StorageIsPersistent"] = "false";

        var result = await ApiProcess.RunAsync(environment, StartTimeout, output => output.Contains("Application started"));

        Assert.AreEqual(-2, result.ExitCode, result.Output);
        Assert.Contains("Profile images may be lost when the container is replaced or recreated", result.Output);
    }

    [TestMethod]
    public async Task Production_without_a_recovery_delivery_adapter_starts_with_a_structured_warning()
    {
        var environment = ValidEnvironment("Production");
        environment["PasswordRecovery__DeliveryFile"] = Path.Combine(Path.GetTempPath(), $"recovery-{Guid.NewGuid():N}.jsonl");

        var result = await ApiProcess.RunAsync(environment, StartTimeout, output => output.Contains("Application started"));

        Assert.AreEqual(-2, result.ExitCode, result.Output);
        // The file adapter is a Development/Testing aid: even when configured it must not count as a Production channel.
        Assert.Contains("Password recovery delivery is not configured", result.Output);
    }

    [TestMethod]
    public async Task Development_starts_without_a_signing_key_or_key_ring_using_ephemeral_values()
    {
        var environment = ValidEnvironment("Development");
        environment["Sessions__Signing__PrivateKeyPem"] = null;
        environment["DataProtection__KeysPath"] = null;
        environment["ProfileImages__StorageIsPersistent"] = null;

        var result = await ApiProcess.RunAsync(environment, StartTimeout, output => output.Contains("Application started"));

        Assert.AreEqual(-2, result.ExitCode, result.Output);
        Assert.Contains("ephemeral development key", result.Output);
        Assert.DoesNotContain("Startup refused", result.Output);
    }

    [TestMethod]
    public async Task Testing_may_omit_the_key_ring_but_never_the_signing_key()
    {
        var withoutKeyRing = ValidEnvironment("Testing");
        withoutKeyRing["DataProtection__KeysPath"] = null;
        withoutKeyRing["ProfileImages__StorageIsPersistent"] = null;
        var started = await ApiProcess.RunAsync(withoutKeyRing, StartTimeout, output => output.Contains("Application started"));
        Assert.AreEqual(-2, started.ExitCode, started.Output);

        var withoutSigningKey = ValidEnvironment("Testing");
        withoutSigningKey["Sessions__Signing__PrivateKeyPem"] = null;
        var refused = await ApiProcess.RunAsync(withoutSigningKey, RefusalTimeout);
        AssertRefused(refused, "Sessions:Signing:PrivateKeyPem");
    }

    private static Dictionary<string, string?> ValidEnvironment(string environmentName) => ProductionApiEnvironment.Create(environmentName);

    private static void AssertRefused((int ExitCode, string Output, bool TimedOut) result, string expectedSetting)
    {
        Assert.IsFalse(result.TimedOut, $"The API kept running instead of refusing. Output:{Environment.NewLine}{result.Output}");
        Assert.AreEqual(StartupFailureReporter.ExitCode, result.ExitCode, result.Output);
        Assert.Contains(expectedSetting, result.Output);
        Assert.Contains("Startup refused", result.Output);
        Assert.DoesNotContain(Sentinel, result.Output);
        Assert.DoesNotContain("not_a_secret", result.Output);
        Assert.DoesNotContain("BEGIN PRIVATE KEY", result.Output);
        Assert.DoesNotContain("Unhandled exception", result.Output);
        Assert.DoesNotContain("   at ", result.Output);
    }
}
