using GaussAuth.Domain.Users;

namespace GaussAuth.Api.Users;

public sealed record UserProfileResponse(
    string FirstName,
    string LastName,
    string DisplayName,
    string? PhoneNumber,
    string? AvatarReference,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public static UserProfileResponse FromDomain(UserProfile profile) => new(
        profile.FirstName,
        profile.LastName,
        profile.DisplayName,
        profile.PhoneNumber,
        profile.AvatarReference,
        profile.CreatedAt,
        profile.UpdatedAt);
}
