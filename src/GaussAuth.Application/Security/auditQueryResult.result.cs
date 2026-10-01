using GaussAuth.Domain.Security;

namespace GaussAuth.Application.Security;

public sealed class AuditQueryResult
{
    public IReadOnlyList<SecurityEvent> Items { get; private init; } = [];
    public string? NextCursor { get; private init; }
    public string? Failure { get; private init; }

    public static AuditQueryResult Success(IReadOnlyList<SecurityEvent> items, string? nextCursor) => new() { Items = items, NextCursor = nextCursor };
    public static AuditQueryResult Invalid() => new() { Failure = "invalid" };
    public static AuditQueryResult Unauthorized() => new() { Failure = "unauthorized" };
    public static AuditQueryResult Forbidden() => new() { Failure = "forbidden" };
}
