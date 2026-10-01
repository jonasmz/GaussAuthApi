using GaussAuth.Domain.Authorization;

namespace GaussAuth.Api.Authorization;

public sealed record UserRoleResponse(Guid Id, Guid ApplicationId, Guid UserId, Guid RoleId, bool IsActive, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt)
{ public static UserRoleResponse FromDomain(UserRole item) => new(item.Id, item.ApplicationId, item.UserId, item.RoleId, item.IsActive, item.CreatedAt, item.UpdatedAt); }
