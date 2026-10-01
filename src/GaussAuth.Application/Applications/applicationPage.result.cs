using DomainApplication = GaussAuth.Domain.Applications.Application;

namespace GaussAuth.Application.Applications;

public sealed class ApplicationPage
{
    public IReadOnlyList<DomainApplication> Items { get; private init; } = [];
    public string? NextCursor { get; private init; }
    public bool IsInvalid { get; private init; }
    public static ApplicationPage Success(IReadOnlyList<DomainApplication> items, string? nextCursor) => new() { Items = items, NextCursor = nextCursor };
    public static ApplicationPage Invalid() => new() { IsInvalid = true };
}
