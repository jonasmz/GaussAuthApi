using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace GaussAuth.Infrastructure.Configuration;

/// <summary>
/// Location of the persisted key ring that protects state such as password-reset credentials. Required in
/// Production-class environments; optional (ephemeral ring) in Development and Testing. The service validates that the
/// location is usable but cannot verify that it survives container recreation — that is the operator's responsibility.
/// </summary>
public sealed class KeyRingOptions
{
    public const string KeysPathKey = "DataProtection:KeysPath";

    /// <summary>Absolute directory holding the key ring, or null when an ephemeral ring is in use.</summary>
    public string? KeysPath { get; init; }

    public bool IsPersisted => KeysPath is not null;

    public static KeyRingOptions Load(IConfiguration configuration, IHostEnvironment? environment)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return KeyRingOptionsValidator.Validate(configuration[KeysPathKey], environment);
    }
}
