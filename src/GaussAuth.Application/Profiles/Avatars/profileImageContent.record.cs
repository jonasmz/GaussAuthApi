namespace GaussAuth.Application.Profiles.Avatars;

/// <summary>Readable stored image plus its trusted media type. The caller disposes the stream.</summary>
public sealed record ProfileImageContent(Stream Content, string MediaType, long Length);
