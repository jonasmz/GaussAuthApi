using GaussAuth.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace GaussAuth.Api.Startup;

/// <summary>
/// Reports a fatal startup configuration failure as one structured Critical log entry per offending setting and
/// returns the process exit code. Only the setting key and a value-free reason are written; no stack trace and no
/// configured value (which may be a secret) ever reaches the output. Options validators must format their failures
/// as <c>&lt;setting&gt;: &lt;reason&gt;</c> without values.
/// </summary>
public static class StartupFailureReporter
{
    public const int ExitCode = 1;

    public static int Report(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddJsonConsole());
        var logger = loggerFactory.CreateLogger("GaussAuth.Startup");

        switch (exception)
        {
            case StartupConfigurationException configuration:
                logger.LogCritical(
                    "Startup refused: invalid or missing configuration. Setting {Setting}: {Reason}",
                    configuration.Setting,
                    configuration.Reason);
                break;
            case OptionsValidationException options:
                foreach (var failure in options.Failures)
                {
                    logger.LogCritical("Startup refused: invalid or missing configuration. {Failure}", failure);
                }

                break;
            default:
                logger.LogCritical("Startup refused: invalid or missing configuration.");
                break;
        }

        return ExitCode;
    }
}
