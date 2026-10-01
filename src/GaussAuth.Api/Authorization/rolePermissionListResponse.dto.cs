namespace GaussAuth.Api.Authorization;

public sealed record RolePermissionListResponse(IReadOnlyList<RolePermissionResponse> Items, string? NextCursor);
