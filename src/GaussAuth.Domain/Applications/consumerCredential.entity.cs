namespace GaussAuth.Domain.Applications;

/// <summary>
/// Managed consumer credential of one Application: the current hash and, while a rotation is being rolled out, the
/// previous (retiring) hash. Holds only one-way hashes, never the plaintext secret.
/// </summary>
public sealed class ConsumerCredential
{
    public const int MaximumHashLength = 512;

    public Guid ApplicationId { get; private set; }
    public string CurrentHash { get; private set; } = string.Empty;
    public string? RetiringHash { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? RotatedAtUtc { get; private set; }
    public DateTimeOffset? RetiredAtUtc { get; private set; }

    private ConsumerCredential() { }

    private ConsumerCredential(Guid applicationId, string currentHash, string? retiringHash, DateTimeOffset now, DateTimeOffset? rotatedAt)
    {
        if (applicationId == Guid.Empty) throw new ArgumentException("Application identifier must be present.", nameof(applicationId));
        ValidateHash(currentHash, nameof(currentHash));
        if (retiringHash is not null)
        {
            ValidateHash(retiringHash, nameof(retiringHash));
            if (string.Equals(retiringHash, currentHash, StringComparison.Ordinal)) throw new ArgumentException("Retiring hash must differ from the current hash.", nameof(retiringHash));
        }

        ApplicationId = applicationId; CurrentHash = currentHash; RetiringHash = retiringHash; CreatedAtUtc = now; RotatedAtUtc = rotatedAt;
    }

    public static ConsumerCredential Create(Guid applicationId, string currentHash, DateTimeOffset now) => new(applicationId, currentHash, null, now, null);

    /// <summary>First rotation of an Application that only had configured hashes: the configured current hash keeps working as the retiring one.</summary>
    public static ConsumerCredential CreateFromConfigured(Guid applicationId, string configuredCurrentHash, string newHash, DateTimeOffset now) =>
        new(applicationId, newHash, configuredCurrentHash, now, now);

    /// <summary>The previous current hash becomes retiring; any older retiring hash is discarded.</summary>
    public void Rotate(string newHash, DateTimeOffset now)
    {
        ValidateHash(newHash, nameof(newHash));
        if (string.Equals(newHash, CurrentHash, StringComparison.Ordinal)) throw new ArgumentException("New hash must differ from the current hash.", nameof(newHash));
        RetiringHash = CurrentHash; CurrentHash = newHash; RotatedAtUtc = now;
    }

    /// <summary>Clears the retiring hash only. Does nothing when there is none.</summary>
    public void RetirePrevious(DateTimeOffset now)
    {
        if (RetiringHash is null) return;
        RetiringHash = null; RetiredAtUtc = now;
    }

    private static void ValidateHash(string hash, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(hash) || hash.Length > MaximumHashLength) throw new ArgumentException("Credential hash is invalid.", parameterName);
    }
}
