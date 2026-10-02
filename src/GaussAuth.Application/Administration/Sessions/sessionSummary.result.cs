using GaussAuth.Domain.Sessions;

namespace GaussAuth.Application.Administration.Sessions;

/// <summary>Safe session view: identifiers, times, and derived state only; never any credential.</summary>
public sealed record SessionSummary(Guid Id, Guid UserId, Guid ApplicationId, DateTimeOffset CreatedAtUtc, DateTimeOffset ExpiresAtUtc, DateTimeOffset? RevokedAtUtc, SessionState State)
{
    public static SessionSummary From(Session session, DateTimeOffset now) => new(
        session.Id, session.UserId, session.ApplicationId, session.CreatedAt.ToUniversalTime(), session.ExpiresAt.ToUniversalTime(),
        session.RevokedAt?.ToUniversalTime(), session.GetState(now));
}
