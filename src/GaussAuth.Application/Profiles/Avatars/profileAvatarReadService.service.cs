using GaussAuth.Application.Profiles.Avatars.Ports;

namespace GaussAuth.Application.Profiles.Avatars;

public sealed class ProfileAvatarReadService(IProfileImageStorage storage)
{
    /// <summary>
    /// Opens a stored avatar only for a strictly parsed opaque reference. Malformed, unknown, and
    /// retired references are indistinguishable (null); no path or filename is ever derived from input.
    /// </summary>
    public async Task<ProfileImageContent?> OpenAsync(string? reference, CancellationToken cancellationToken)
    {
        if (!ProfileImageReference.TryParse(reference, out var parsed))
        {
            return null;
        }

        return await storage.OpenReadAsync(parsed, cancellationToken);
    }
}
