using System.Globalization;
using GaussAuth.Application.Applications.Ports;
using GaussAuth.Application.Sessions;
using GaussAuth.Application.Sessions.Ports;
using GaussAuth.Application.Users.Ports;
using GaussAuth.Domain.Sessions;

namespace GaussAuth.Application.Administration.Sessions;

/// <summary>
/// Administrative session inspection and revocation. Every revocation goes through <see cref="SessionService.RevokeAsync"/>,
/// which records one actor-stamped <c>SessionRevoked</c> event per session; bulk operations are capped.
/// </summary>
public sealed class AdministrativeSessionService(
    ISessionRepository sessions,
    SessionService sessionService,
    IApplicationRepository applications,
    IUserRepository users,
    AdministrationOptions options,
    TimeProvider timeProvider)
{
    private const int DefaultLimit = 50;
    private const int MaximumLimit = 100;

    public async Task<AdministrativeSessionPage> ListAsync(Guid? applicationId, Guid? userId, string? state, string? cursor, int? limit, CancellationToken ct)
    {
        var take = limit ?? DefaultLimit;
        if (take is < 1 or > MaximumLimit) return AdministrativeSessionPage.Invalid();
        SessionState? parsedState = null;
        if (state is not null)
        {
            if (!Enum.TryParse<SessionState>(state, ignoreCase: true, out var value) || !Enum.IsDefined(value) || state.Length > 16) return AdministrativeSessionPage.Invalid();
            parsedState = value;
        }

        (DateTimeOffset CreatedAt, Guid Id)? after = null;
        if (cursor is not null)
        {
            if (!TryParseCursor(cursor, out var parsed)) return AdministrativeSessionPage.Invalid();
            after = parsed;
        }

        if (applicationId is { } application && await applications.GetByIdAsync(application, ct) is null) return AdministrativeSessionPage.NotFound();
        if (userId is { } user && await users.GetByIdAsync(user, ct) is null) return AdministrativeSessionPage.NotFound();

        var now = timeProvider.GetUtcNow();
        var items = await sessions.ListAsync(userId, applicationId, parsedState, now, after, take, ct);
        var next = items.Count == take ? FormatCursor(items[^1]) : null;
        return AdministrativeSessionPage.Success(items.Select(item => SessionSummary.From(item, now)).ToArray(), next);
    }

    public async Task<AdministrativeSessionOperationResult> GetAsync(Guid applicationId, Guid sessionId, CancellationToken ct)
    {
        var session = await sessions.GetByIdAsync(sessionId, ct);
        return session is null || session.ApplicationId != applicationId
            ? AdministrativeSessionOperationResult.NotFound()
            : AdministrativeSessionOperationResult.Success(SessionSummary.From(session, timeProvider.GetUtcNow()));
    }

    public async Task<AdministrativeSessionOperationResult> RevokeAsync(Guid applicationId, Guid sessionId, CancellationToken ct)
    {
        var session = await sessions.GetByIdAsync(sessionId, ct);
        if (session is null || session.ApplicationId != applicationId) return AdministrativeSessionOperationResult.NotFound();
        await sessionService.RevokeAsync(sessionId, ct);
        return AdministrativeSessionOperationResult.Success(SessionSummary.From(session, timeProvider.GetUtcNow()));
    }

    public async Task<BulkSessionRevocationResult> RevokeUserInApplicationAsync(Guid applicationId, Guid userId, CancellationToken ct)
    {
        if (await applications.GetByIdAsync(applicationId, ct) is null || await users.GetByIdAsync(userId, ct) is null) return BulkSessionRevocationResult.NotFound();
        return await RevokeBoundedAsync(userId, applicationId, ct);
    }

    public async Task<BulkSessionRevocationResult> RevokeAllInApplicationAsync(Guid applicationId, CancellationToken ct)
    {
        if (await applications.GetByIdAsync(applicationId, ct) is null) return BulkSessionRevocationResult.NotFound();
        return await RevokeBoundedAsync(null, applicationId, ct);
    }

    public async Task<BulkSessionRevocationResult> RevokeUserEverywhereAsync(Guid userId, CancellationToken ct)
    {
        if (await users.GetByIdAsync(userId, ct) is null) return BulkSessionRevocationResult.NotFound();
        return await RevokeBoundedAsync(userId, null, ct);
    }

    private async Task<BulkSessionRevocationResult> RevokeBoundedAsync(Guid? userId, Guid? applicationId, CancellationToken ct)
    {
        var max = options.MaxBulkSessionRevocation;
        var active = await sessions.ListActiveAsync(userId, applicationId, timeProvider.GetUtcNow(), max + 1, ct);
        var batch = active.Take(max).ToArray();
        foreach (var session in batch) await sessionService.RevokeAsync(session.Id, ct);
        return BulkSessionRevocationResult.Success(batch.Length, active.Count > max);
    }

    private static string FormatCursor(Session session) => $"{session.CreatedAt.UtcTicks.ToString(CultureInfo.InvariantCulture)}.{session.Id:N}";

    private static bool TryParseCursor(string cursor, out (DateTimeOffset CreatedAt, Guid Id) value)
    {
        value = default;
        if (cursor.Length > 64) return false;
        var parts = cursor.Split('.');
        if (parts.Length != 2 || !long.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var ticks) ||
            ticks > DateTime.MaxValue.Ticks || !Guid.TryParseExact(parts[1], "N", out var id)) return false;
        value = (new DateTimeOffset(ticks, TimeSpan.Zero), id);
        return true;
    }
}
