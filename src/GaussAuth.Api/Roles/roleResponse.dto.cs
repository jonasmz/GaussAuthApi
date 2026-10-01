using GaussAuth.Domain.Roles;

namespace GaussAuth.Api.Roles;

public sealed record RoleResponse(Guid Id, Guid ApplicationId, string Name, string? Description, bool IsActive, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt)
{ public static RoleResponse FromDomain(Role item) => new(item.Id, item.ApplicationId, item.Name, item.Description, item.IsActive, item.CreatedAt, item.UpdatedAt); }
