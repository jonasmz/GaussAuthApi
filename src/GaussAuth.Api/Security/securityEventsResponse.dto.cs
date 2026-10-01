namespace GaussAuth.Api.Security;

public sealed record SecurityEventsResponse(IReadOnlyList<SecurityEventResponse> Items, string? NextCursor);
