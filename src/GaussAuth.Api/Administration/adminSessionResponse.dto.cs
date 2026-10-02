using GaussAuth.Application.Administration.Sessions;

namespace GaussAuth.Api.Administration;

public sealed record AdminSessionResponse(Guid Id, Guid UserId, Guid ApplicationId, DateTimeOffset CreatedAtUtc, DateTimeOffset ExpiresAtUtc, DateTimeOffset? RevokedAtUtc, string State)
{
    public static AdminSessionResponse FromResult(SessionSummary summary) => new(
        summary.Id, summary.UserId, summary.ApplicationId, summary.CreatedAtUtc, summary.ExpiresAtUtc, summary.RevokedAtUtc, summary.State.ToString().ToLowerInvariant());
}
