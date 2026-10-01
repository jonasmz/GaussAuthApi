namespace GaussAuth.Api.Authorization;

public sealed record EffectivePermissionListResponse(IReadOnlyList<EffectivePermissionResponse> Items);
