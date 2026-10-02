using System.Globalization;
using Microsoft.Extensions.Configuration;

namespace GaussAuth.Infrastructure.Configuration;

/// <summary>
/// Reads typed, range-checked configuration values and reports problems as <see cref="StartupConfigurationException"/>
/// naming only the key. The framework binder is deliberately avoided because its conversion errors can carry the
/// offending value, which may be sensitive. A missing or blank value yields the supplied default.
/// </summary>
public static class ConfigurationValueReader
{
    public static int GetBoundedInt32(this IConfiguration configuration, string key, int defaultValue, int minimum, int maximum)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var raw = configuration[key];
        if (string.IsNullOrWhiteSpace(raw)) return defaultValue;
        if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
            throw new StartupConfigurationException(key, "must be an integer");
        if (value < minimum || value > maximum)
            throw new StartupConfigurationException(key, $"must be between {minimum} and {maximum}");
        return value;
    }

    public static long GetBoundedInt64(this IConfiguration configuration, string key, long defaultValue, long minimum, long maximum)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var raw = configuration[key];
        if (string.IsNullOrWhiteSpace(raw)) return defaultValue;
        if (!long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
            throw new StartupConfigurationException(key, "must be an integer");
        if (value < minimum || value > maximum)
            throw new StartupConfigurationException(key, $"must be between {minimum} and {maximum}");
        return value;
    }

    /// <summary>Returns null when the value is missing or blank, otherwise the range-checked integer.</summary>
    public static int? GetOptionalBoundedInt32(this IConfiguration configuration, string key, int minimum, int maximum)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return string.IsNullOrWhiteSpace(configuration[key]) ? null : configuration.GetBoundedInt32(key, 0, minimum, maximum);
    }

    /// <summary>Returns null when the value is missing or blank, otherwise the parsed boolean.</summary>
    public static bool? GetOptionalBoolean(this IConfiguration configuration, string key)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var raw = configuration[key];
        if (string.IsNullOrWhiteSpace(raw)) return null;
        if (!bool.TryParse(raw, out var value))
            throw new StartupConfigurationException(key, "must be true or false");
        return value;
    }
}
