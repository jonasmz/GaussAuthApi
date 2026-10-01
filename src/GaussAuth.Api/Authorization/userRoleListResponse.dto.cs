namespace GaussAuth.Api.Authorization;

public sealed record UserRoleListResponse(IReadOnlyList<UserRoleResponse> Items, string? NextCursor);
