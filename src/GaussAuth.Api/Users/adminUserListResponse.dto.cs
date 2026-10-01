namespace GaussAuth.Api.Users;

public sealed record AdminUserListResponse(IReadOnlyList<AdminUserSummaryResponse> Items, string? NextCursor);
