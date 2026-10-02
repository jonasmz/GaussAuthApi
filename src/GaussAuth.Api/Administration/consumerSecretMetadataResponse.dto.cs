using GaussAuth.Application.Administration.ConsumerCredentials;

namespace GaussAuth.Api.Administration;

public sealed record ConsumerSecretMetadataResponse(bool Exists, string? Source, DateTimeOffset? CreatedAtUtc, DateTimeOffset? RotatedAtUtc, bool HasRetiring)
{
    public static ConsumerSecretMetadataResponse FromResult(ConsumerSecretMetadata metadata) =>
        new(metadata.Exists, metadata.Source, metadata.CreatedAtUtc, metadata.RotatedAtUtc, metadata.HasRetiring);
}
