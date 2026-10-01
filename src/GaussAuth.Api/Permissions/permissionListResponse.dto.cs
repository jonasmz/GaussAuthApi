namespace GaussAuth.Api.Permissions;

public sealed record PermissionListResponse(IReadOnlyList<PermissionResponse> Items, string? NextCursor);
