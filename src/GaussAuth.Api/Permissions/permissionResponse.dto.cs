using DomainPermission = GaussAuth.Domain.Permissions.Permission;

namespace GaussAuth.Api.Permissions;

public sealed record PermissionResponse(Guid Id, Guid ApplicationId, string Code, string? Description, bool IsActive, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt)
{ public static PermissionResponse FromDomain(DomainPermission item) => new(item.Id, item.ApplicationId, item.Code, item.Description, item.IsActive, item.CreatedAt, item.UpdatedAt); }
