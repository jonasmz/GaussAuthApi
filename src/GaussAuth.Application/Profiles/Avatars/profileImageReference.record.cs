using System.Globalization;

namespace GaussAuth.Application.Profiles.Avatars;

/// <summary>
/// Opaque, generated, versioned avatar reference: 32 lowercase hex characters plus a fixed
/// allow-listed extension. Never derived from user input, and the only form accepted by storage.
/// </summary>
public sealed record ProfileImageReference
{
    private const int TokenLength = 32;

    public string Value { get; }

    public ProfileImageFormat Format { get; }

    private ProfileImageReference(string value, ProfileImageFormat format)
    {
        Value = value;
        Format = format;
    }

    public string MediaType => MediaTypeFor(Format);

    public static ProfileImageReference Create(ProfileImageFormat format) =>
        new(Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture) + ExtensionFor(format), format);

    public static bool TryParse(string? value, out ProfileImageReference reference)
    {
        reference = null!;
        if (value is null || value.Length < TokenLength + 4 || value.Length > TokenLength + 5)
        {
            return false;
        }

        for (var index = 0; index < TokenLength; index++)
        {
            var character = value[index];
            if (!(character is >= '0' and <= '9' || character is >= 'a' and <= 'f'))
            {
                return false;
            }
        }

        var format = value[TokenLength..] switch
        {
            ".jpg" => ProfileImageFormat.Jpeg,
            ".png" => ProfileImageFormat.Png,
            ".webp" => ProfileImageFormat.WebP,
            _ => (ProfileImageFormat?)null
        };

        if (format is null)
        {
            return false;
        }

        reference = new ProfileImageReference(value, format.Value);
        return true;
    }

    public static string MediaTypeFor(ProfileImageFormat format) => format switch
    {
        ProfileImageFormat.Jpeg => "image/jpeg",
        ProfileImageFormat.Png => "image/png",
        ProfileImageFormat.WebP => "image/webp",
        _ => throw new ArgumentOutOfRangeException(nameof(format))
    };

    private static string ExtensionFor(ProfileImageFormat format) => format switch
    {
        ProfileImageFormat.Jpeg => ".jpg",
        ProfileImageFormat.Png => ".png",
        ProfileImageFormat.WebP => ".webp",
        _ => throw new ArgumentOutOfRangeException(nameof(format))
    };
}
