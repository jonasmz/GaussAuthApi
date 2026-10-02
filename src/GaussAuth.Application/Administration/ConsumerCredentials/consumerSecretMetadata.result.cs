namespace GaussAuth.Application.Administration.ConsumerCredentials;

/// <summary>Non-sensitive description of an Application's consumer credential. Never carries a value, prefix, length, or hash.</summary>
public sealed record ConsumerSecretMetadata(bool Exists, string? Source, DateTimeOffset? CreatedAtUtc, DateTimeOffset? RotatedAtUtc, bool HasRetiring)
{
    public const string ManagedSource = "managed";
    public const string ConfiguredSource = "configured";
}
