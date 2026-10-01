using DomainPermission = GaussAuth.Domain.Permissions.Permission;

namespace GaussAuth.Api.Authorization;

public sealed record EffectivePermissionResponse(Guid Id, string Code, string? Description)
{ public static EffectivePermissionResponse FromDomain(DomainPermission item) => new(item.Id, item.Code, item.Description); }
