namespace GaussAuth.Application.Profiles.Avatars.Ports;

public interface IProfileImageStorage
{
    /// <summary>Writes sanitized content to temporary storage under a generated opaque name.</summary>
    Task<StagedProfileImage> StageAsync(ProcessedProfileImage image, CancellationToken cancellationToken);

    /// <summary>Moves a staged image to its final location; the staged copy no longer exists afterwards.</summary>
    Task PromoteAsync(StagedProfileImage staged, CancellationToken cancellationToken);

    /// <summary>Best-effort removal of an unpromoted staged image. Returns false when removal failed.</summary>
    Task<bool> DiscardStagedAsync(StagedProfileImage staged, CancellationToken cancellationToken);

    /// <summary>Best-effort removal of a promoted image. Returns false when removal failed; absence counts as success.</summary>
    Task<bool> DeleteAsync(ProfileImageReference reference, CancellationToken cancellationToken);

    /// <summary>Opens a promoted image for reading, or returns null when it is not stored.</summary>
    Task<ProfileImageContent?> OpenReadAsync(ProfileImageReference reference, CancellationToken cancellationToken);
}
