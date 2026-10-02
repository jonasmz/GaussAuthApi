namespace GaussAuth.Api.Administration;

public sealed record AdminSessionListResponse(IReadOnlyList<AdminSessionResponse> Items, string? NextCursor);
