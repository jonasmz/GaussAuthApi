namespace GaussAuth.Application.Profiles.Avatars;

/// <summary>A sanitized image written to temporary storage but not yet referenced by any profile.</summary>
public sealed record StagedProfileImage(ProfileImageReference Reference, string StagingToken);
