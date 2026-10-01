using GaussAuth.Domain.Users;

namespace GaussAuth.Api.Users;

/// <summary>Minimal user view for administrative listings: no profile, credential, or security material.</summary>
public sealed record AdminUserSummaryResponse(Guid Id, string Email, bool IsActive, DateTimeOffset CreatedAtUtc)
{
    public static AdminUserSummaryResponse FromDomain(User user) => new(user.Id, user.Email, user.IsActive, user.CreatedAt.ToUniversalTime());
}
