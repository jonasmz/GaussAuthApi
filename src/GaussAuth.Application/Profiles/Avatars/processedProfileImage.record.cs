namespace GaussAuth.Application.Profiles.Avatars;

/// <summary>Sanitized, metadata-free image bytes ready for storage.</summary>
public sealed record ProcessedProfileImage(byte[] Content, ProfileImageFormat Format, int Width, int Height);
