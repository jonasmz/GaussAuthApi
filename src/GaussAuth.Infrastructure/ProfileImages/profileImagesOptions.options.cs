using GaussAuth.Application.Profiles.Avatars;
using GaussAuth.Infrastructure.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace GaussAuth.Infrastructure.ProfileImages;

public sealed class ProfileImagesOptions
{
    public const string SectionName = "ProfileImages";
    public const string RootPathKey = "ProfileImages:RootPath";
    public const string StorageIsPersistentKey = "ProfileImages:StorageIsPersistent";
    public const string RequirePersistenceDeclarationKey = "ProfileImages:RequirePersistenceDeclaration";

    public string RootPath { get; init; } = string.Empty;

    public long MaxBytes { get; init; } = ProfileImageLimits.DefaultMaxBytes;

    public int MaxDimension { get; init; } = ProfileImageLimits.DefaultMaxDimension;

    /// <summary>
    /// The operator's declaration that <see cref="RootPath"/> lives on storage that survives container replacement, or
    /// null when no declaration was made (allowed only in Development/Testing). The service never infers this from the path.
    /// </summary>
    public bool? StorageIsPersistent { get; init; }

    public ProfileImageLimits Limits => new(MaxBytes, MaxDimension);

    public static ProfileImagesOptions Load(IConfiguration configuration, IHostEnvironment? environment)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var productionClass = environment is null || environment.IsProductionClass();

        var maxBytes = configuration.GetBoundedInt64($"{SectionName}:MaxBytes", ProfileImageLimits.DefaultMaxBytes, 1, long.MaxValue);
        var maxDimension = configuration.GetBoundedInt32($"{SectionName}:MaxDimension", ProfileImageLimits.DefaultMaxDimension, 1, int.MaxValue);

        var root = configuration[RootPathKey];
        if (string.IsNullOrWhiteSpace(root))
        {
            if (productionClass)
                throw new StartupConfigurationException(RootPathKey, "is required: configure an absolute directory for profile images");

            root = Path.Combine(Directory.GetCurrentDirectory(), ".profile-images");
        }

        if (root.Contains('\0') || !Path.IsPathRooted(root))
            throw new StartupConfigurationException(RootPathKey, "must be an absolute directory path");

        var persistent = configuration.GetOptionalBoolean(StorageIsPersistentKey);
        var requireDeclaration = productionClass || configuration.GetOptionalBoolean(RequirePersistenceDeclarationKey) == true;
        if (requireDeclaration && persistent is null)
        {
            throw new StartupConfigurationException(
                StorageIsPersistentKey,
                "is required: declare true or false whether the profile image storage survives container replacement");
        }

        return new ProfileImagesOptions
        {
            RootPath = Path.GetFullPath(root),
            MaxBytes = maxBytes,
            MaxDimension = maxDimension,
            StorageIsPersistent = persistent
        };
    }
}
