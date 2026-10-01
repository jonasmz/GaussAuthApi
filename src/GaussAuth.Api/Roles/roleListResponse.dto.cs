namespace GaussAuth.Api.Roles;

public sealed record RoleListResponse(IReadOnlyList<RoleResponse> Items, string? NextCursor);
