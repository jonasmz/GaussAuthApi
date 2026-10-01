namespace GaussAuth.Application.Profiles.Avatars.Ports;

public interface IProfileImageProcessor
{
    /// <summary>
    /// Inspects actual content, enforces configured limits, strips metadata and re-encodes into an
    /// allowed format. Never trusts a client filename or declared media type.
    /// </summary>
    Task<ProfileImageProcessingResult> ProcessAsync(Stream input, CancellationToken cancellationToken);
}
