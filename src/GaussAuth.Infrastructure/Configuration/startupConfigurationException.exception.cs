namespace GaussAuth.Infrastructure.Configuration;

/// <summary>
/// A critical configuration problem detected while starting the service. It carries only the setting key and a
/// value-free reason so that operators learn what to fix without any configured value (which may be a secret)
/// ever reaching logs or process output.
/// </summary>
public sealed class StartupConfigurationException : Exception
{
    public StartupConfigurationException(string setting, string reason)
        : base(BuildMessage(setting, reason))
    {
        Setting = setting;
        Reason = reason;
    }

    /// <summary>The configuration key (for example <c>DataProtection:KeysPath</c>) that is missing or invalid.</summary>
    public string Setting { get; }

    /// <summary>Why the setting is unacceptable. Never contains the configured value.</summary>
    public string Reason { get; }

    private static string BuildMessage(string setting, string reason)
    {
        if (string.IsNullOrWhiteSpace(setting)) throw new ArgumentException("A setting key is required.", nameof(setting));
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("A reason is required.", nameof(reason));
        return $"{setting}: {reason}";
    }
}
