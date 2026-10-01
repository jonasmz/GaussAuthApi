using GaussAuth.Domain.Users;

namespace GaussAuth.Api.Users;

public sealed record UserResponse(
    Guid Id,
    string Email,
    string NormalizedEmail,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    UserProfileResponse Profile)
{
    public static UserResponse FromDomain(User user) => new(
        user.Id,
        user.Email,
        user.NormalizedEmail,
        user.IsActive,
        user.CreatedAt,
        user.UpdatedAt,
        UserProfileResponse.FromDomain(user.Profile));
}
