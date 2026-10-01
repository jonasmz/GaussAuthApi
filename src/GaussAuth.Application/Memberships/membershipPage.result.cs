using GaussAuth.Domain.Memberships;

namespace GaussAuth.Application.Memberships;

public sealed class MembershipPage
{
    public IReadOnlyList<ApplicationMembership> Items { get; private init; } = [];
    public string? NextCursor { get; private init; }
    public string? Failure { get; private init; }
    public static MembershipPage Success(IReadOnlyList<ApplicationMembership> items, string? cursor) => new() { Items = items, NextCursor = cursor };
    public static MembershipPage Invalid() => new() { Failure = "invalid" };
    public static MembershipPage UserNotFound() => new() { Failure = "user-not-found" };
    public static MembershipPage ApplicationNotFound() => new() { Failure = "application-not-found" };
}
