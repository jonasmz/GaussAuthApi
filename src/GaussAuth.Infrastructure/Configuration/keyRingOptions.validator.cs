using Microsoft.Extensions.Hosting;

namespace GaussAuth.Infrastructure.Configuration;

public static class KeyRingOptionsValidator
{
    public static KeyRingOptions Validate(string? path, IHostEnvironment? environment)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            if (environment is null || environment.IsProductionClass())
            {
                throw new StartupConfigurationException(
                    KeyRingOptions.KeysPathKey,
                    "is required: configure an absolute directory on storage that survives application and container recreation");
            }

            return new KeyRingOptions();
        }

        if (path.Contains('\0') || !Path.IsPathRooted(path))
            throw new StartupConfigurationException(KeyRingOptions.KeysPathKey, "must be an absolute directory path");

        var full = Path.GetFullPath(path);
        try
        {
            Directory.CreateDirectory(full);
            var probe = Path.Combine(full, $".write-probe-{Guid.NewGuid():N}");
            File.WriteAllBytes(probe, []);
            File.Delete(probe);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
        {
            throw new StartupConfigurationException(KeyRingOptions.KeysPathKey, "must be a directory the service can create and write to");
        }

        return new KeyRingOptions { KeysPath = full };
    }
}
