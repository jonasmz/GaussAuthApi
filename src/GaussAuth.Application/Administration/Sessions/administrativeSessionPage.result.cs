namespace GaussAuth.Application.Administration.Sessions;

public sealed class AdministrativeSessionPage
{
    public IReadOnlyList<SessionSummary> Items { get; private init; } = [];
    public string? NextCursor { get; private init; }
    public string? Failure { get; private init; }

    public static AdministrativeSessionPage Success(IReadOnlyList<SessionSummary> items, string? nextCursor) => new() { Items = items, NextCursor = nextCursor };
    public static AdministrativeSessionPage Invalid() => new() { Failure = "invalid" };
    public static AdministrativeSessionPage NotFound() => new() { Failure = "not-found" };
}
