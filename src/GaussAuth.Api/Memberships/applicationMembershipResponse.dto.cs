using GaussAuth.Domain.Memberships;

namespace GaussAuth.Api.Memberships;

public sealed record ApplicationMembershipResponse(Guid Id, Guid UserId, Guid ApplicationId, bool IsActive, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt)
{ public static ApplicationMembershipResponse FromDomain(ApplicationMembership item) => new(item.Id, item.UserId, item.ApplicationId, item.IsActive, item.CreatedAt, item.UpdatedAt); }
