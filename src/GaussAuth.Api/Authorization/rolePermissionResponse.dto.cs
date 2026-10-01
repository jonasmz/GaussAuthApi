using GaussAuth.Domain.Authorization;

namespace GaussAuth.Api.Authorization;

public sealed record RolePermissionResponse(Guid Id, Guid ApplicationId, Guid RoleId, Guid PermissionId, bool IsActive, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt)
{ public static RolePermissionResponse FromDomain(RolePermission item) => new(item.Id, item.ApplicationId, item.RoleId, item.PermissionId, item.IsActive, item.CreatedAt, item.UpdatedAt); }
