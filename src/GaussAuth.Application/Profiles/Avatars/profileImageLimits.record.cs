namespace GaussAuth.Application.Profiles.Avatars;

public sealed record ProfileImageLimits(long MaxBytes, int MaxDimension)
{
    public const long DefaultMaxBytes = 5 * 1024 * 1024;

    public const int DefaultMaxDimension = 4096;

    public static ProfileImageLimits Default { get; } = new(DefaultMaxBytes, DefaultMaxDimension);
}
