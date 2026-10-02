namespace GaussAuth.Application.Administration.Sessions;

/// <summary>Outcome of a bounded bulk revocation: how many sessions were revoked and whether more active sessions remain.</summary>
public sealed class BulkSessionRevocationResult
{
    public int Revoked { get; private init; }
    public bool HasMore { get; private init; }
    public bool IsNotFound { get; private init; }

    public static BulkSessionRevocationResult Success(int revoked, bool hasMore) => new() { Revoked = revoked, HasMore = hasMore };
    public static BulkSessionRevocationResult NotFound() => new() { IsNotFound = true };
}
