using System.Text;
using GaussAuth.Application.Administration.Ports;
using GaussAuth.Application.Authorization.Ports;
using GaussAuth.Application.Security.Ports;
using GaussAuth.Application.Sessions;
using GaussAuth.Domain.Security;

namespace GaussAuth.Application.Security;

public sealed class SecurityEventQueryService(SessionService sessions, IUserRoleRepository userRoles,
    IGlobalAdministratorPolicy globalAdministrators, ISecurityEventQueryRepository repository)
{
    private const string AuditReadPermission = "audit.events.read";

    public async Task<AuditQueryResult> QueryAsync(string accessCredential, SecurityEventQuery query, CancellationToken cancellationToken)
    {
        if (!IsValid(query)) return AuditQueryResult.Invalid();
        var session = await sessions.GetAuthenticatedContextAsync(accessCredential, cancellationToken);
        if (!session.IsSuccess || session.UserId is null || session.ApplicationId is null) return AuditQueryResult.Unauthorized();

        var scope = await ResolveScopeAsync(session.UserId.Value, session.ApplicationId.Value, cancellationToken);
        if (scope is null) return AuditQueryResult.Forbidden();
        if (scope.Scope == AuditQueryScope.Application && query.ApplicationId is { } requested && requested != scope.ApplicationId)
            return AuditQueryResult.Forbidden();

        var effectiveQuery = scope.Scope == AuditQueryScope.Application
            ? query with { ApplicationId = scope.ApplicationId }
            : query;
        var items = await repository.ListAsync(effectiveQuery, scope.ApplicationId, scope.Scope == AuditQueryScope.Global, cancellationToken);
        var page = items.Take(query.PageSize).ToArray();
        var next = items.Count > query.PageSize ? EncodeCursor(page[^1]) : null;
        return AuditQueryResult.Success(page, next);
    }

    private async Task<AuthorizedAuditScope?> ResolveScopeAsync(Guid userId, Guid applicationId, CancellationToken cancellationToken)
    {
        if (globalAdministrators.IsGlobalAdministrator(userId)) return AuthorizedAuditScope.Global();
        var permissions = await userRoles.GetEffectivePermissionsAsync(userId, applicationId, cancellationToken);
        return permissions.Any(permission => string.Equals(permission.Code, AuditReadPermission, StringComparison.Ordinal))
            ? AuthorizedAuditScope.Application(applicationId)
            : null;
    }

    private static bool IsValid(SecurityEventQuery query) =>
        query.PageSize is > 0 and <= 100 &&
        (query.FromUtc is null || query.FromUtc.Value.Offset == TimeSpan.Zero) &&
        (query.ToUtc is null || query.ToUtc.Value.Offset == TimeSpan.Zero) &&
        (query.FromUtc is null || query.ToUtc is null || query.FromUtc <= query.ToUtc) &&
        (query.EventType is null || query.EventType.Length <= 128) &&
        (query.Cursor is null || query.Cursor.Length <= 256 && TryDecodeCursor(query.Cursor, out _));

    public static string EncodeCursor(SecurityEvent securityEvent) => Convert.ToBase64String(Encoding.UTF8.GetBytes($"{securityEvent.OccurredAtUtc.UtcTicks:N}:{securityEvent.Id:N}"));

    public static bool TryDecodeCursor(string cursor, out (DateTimeOffset OccurredAtUtc, Guid Id) value)
    {
        value = default;
        try
        {
            var parts = Encoding.UTF8.GetString(Convert.FromBase64String(cursor)).Split(':');
            if (parts.Length != 2 || !long.TryParse(parts[0], out var ticks) || !Guid.TryParseExact(parts[1], "N", out var id)) return false;
            value = (new DateTimeOffset(ticks, TimeSpan.Zero), id);
            return true;
        }
        catch (FormatException) { return false; }
    }
}
