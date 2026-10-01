namespace GaussAuth.Api.Memberships;

public sealed record ApplicationMembershipListResponse(IReadOnlyList<ApplicationMembershipResponse> Items, string? NextCursor);
