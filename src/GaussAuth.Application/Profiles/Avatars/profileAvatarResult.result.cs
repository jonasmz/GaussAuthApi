using GaussAuth.Domain.Users;

namespace GaussAuth.Application.Profiles.Avatars;

public sealed record ProfileAvatarResult
{
    private ProfileAvatarResult(ProfileAvatarOutcome outcome, UserProfile? profile, ProfileImageRejection? rejection)
    {
        Outcome = outcome;
        Profile = profile;
        Rejection = rejection;
    }

    public ProfileAvatarOutcome Outcome { get; }

    public UserProfile? Profile { get; }

    public ProfileImageRejection? Rejection { get; }

    public static ProfileAvatarResult Success(UserProfile profile) => new(ProfileAvatarOutcome.Success, profile, null);

    public static ProfileAvatarResult NotAuthenticated() => new(ProfileAvatarOutcome.NotAuthenticated, null, null);

    public static ProfileAvatarResult Rejected(ProfileImageRejection rejection) => new(ProfileAvatarOutcome.Rejected, null, rejection);
}
