using GaussAuth.Api.Startup;
using GaussAuth.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace GaussAuth.Foundation.Tests;

[TestClass]
public sealed class StartupFailureReporterTests
{
    [TestMethod]
    public void Configuration_failure_is_reported_with_the_setting_key_only_and_a_non_zero_exit_code()
    {
        var output = Capture(() => StartupFailureReporter.Report(
            new StartupConfigurationException("DataProtection:KeysPath", "is required in this environment")), out var exitCode);

        Assert.AreEqual(StartupFailureReporter.ExitCode, exitCode);
        Assert.AreNotEqual(0, exitCode);
        Assert.Contains("DataProtection:KeysPath", output);
        Assert.Contains("is required in this environment", output);
        Assert.Contains("Critical", output);
        Assert.DoesNotContain("StartupConfigurationException", output);
        Assert.DoesNotContain("   at ", output);
    }

    [TestMethod]
    public void Options_validation_failures_are_each_reported_without_a_stack_trace()
    {
        var failure = new OptionsValidationException(
            "RateLimiting", typeof(object), ["RateLimiting:Login:PermitLimit: must be at least 1", "RateLimiting:Login:WindowSeconds: must be at least 1"]);

        var output = Capture(() => StartupFailureReporter.Report(failure), out var exitCode);

        Assert.AreNotEqual(0, exitCode);
        Assert.Contains("RateLimiting:Login:PermitLimit: must be at least 1", output);
        Assert.Contains("RateLimiting:Login:WindowSeconds: must be at least 1", output);
        Assert.DoesNotContain("OptionsValidationException", output);
        Assert.DoesNotContain("   at ", output);
    }

    [TestMethod]
    public void A_configuration_exception_requires_a_setting_and_a_reason()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new StartupConfigurationException(" ", "reason"));
        Assert.ThrowsExactly<ArgumentException>(() => new StartupConfigurationException("Some:Key", ""));
    }

    private static string Capture(Func<int> action, out int exitCode)
    {
        var original = Console.Out;
        using var writer = new StringWriter();
        Console.SetOut(writer);
        try
        {
            exitCode = action();
        }
        finally
        {
            Console.SetOut(original);
        }

        return writer.ToString();
    }
}
