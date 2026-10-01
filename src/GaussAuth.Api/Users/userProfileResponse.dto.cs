using GaussAuth.Application.Profiles.Avatars;
using GaussAuth.Domain.Users;

namespace GaussAuth.Api.Users;

public sealed record UserProfileResponse(
    string FirstName,
    string LastName,
    string DisplayName,
    string? PhoneNumber,
    string? AvatarReference,
    string? AvatarUrl,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public static UserProfileResponse FromDomain(UserProfile profile) => new(
        profile.FirstName,
        profile.LastName,
        profile.DisplayName,
        profile.PhoneNumber,
        profile.AvatarReference,
        ProfileImageReference.TryParse(profile.AvatarReference, out _) ? $"/profile-images/{profile.AvatarReference}" : null,
        profile.CreatedAt,
        profile.UpdatedAt);
}
