using GaussAuth.Application.Profiles.Avatars;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace GaussAuth.Infrastructure.ProfileImages;

public sealed class ProfileImagesOptions
{
    public const string SectionName = "ProfileImages";

    public string RootPath { get; init; } = string.Empty;

    public long MaxBytes { get; init; } = ProfileImageLimits.DefaultMaxBytes;

    public int MaxDimension { get; init; } = ProfileImageLimits.DefaultMaxDimension;

    public ProfileImageLimits Limits => new(MaxBytes, MaxDimension);

    public static ProfileImagesOptions Load(IConfiguration configuration, IHostEnvironment? environment)
    {
        var section = configuration.GetSection(SectionName);
        long maxBytes;
        int maxDimension;
        try
        {
            maxBytes = string.IsNullOrWhiteSpace(section["MaxBytes"])
                ? ProfileImageLimits.DefaultMaxBytes
                : section.GetValue<long>("MaxBytes");
            maxDimension = string.IsNullOrWhiteSpace(section["MaxDimension"])
                ? ProfileImageLimits.DefaultMaxDimension
                : section.GetValue<int>("MaxDimension");
        }
        catch (InvalidOperationException)
        {
            throw new InvalidOperationException("Profile image configuration is invalid.");
        }

        if (maxBytes <= 0 || maxDimension <= 0)
        {
            throw new InvalidOperationException("Profile image configuration is invalid.");
        }

        var root = section["RootPath"];
        if (string.IsNullOrWhiteSpace(root))
        {
            if (environment is null || !(environment.IsDevelopment() || environment.IsEnvironment("Testing")))
            {
                throw new InvalidOperationException("Profile image storage root configuration is missing or invalid.");
            }

            root = Path.Combine(Directory.GetCurrentDirectory(), ".profile-images");
        }

        if (root.Contains('\0') || !Path.IsPathRooted(root))
        {
            throw new InvalidOperationException("Profile image storage root configuration is missing or invalid.");
        }

        return new ProfileImagesOptions
        {
            RootPath = Path.GetFullPath(root),
            MaxBytes = maxBytes,
            MaxDimension = maxDimension
        };
    }
}
