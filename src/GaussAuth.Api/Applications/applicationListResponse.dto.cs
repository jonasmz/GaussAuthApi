namespace GaussAuth.Api.Applications;

public sealed record ApplicationListResponse(IReadOnlyList<ApplicationResponse> Items, string? NextCursor);
