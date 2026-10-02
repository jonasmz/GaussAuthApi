using GaussAuth.Application.Administration.ConsumerCredentials;

namespace GaussAuth.Api.Administration;

/// <summary>The one response that carries a plaintext consumer secret. It is never returned again.</summary>
public sealed record ConsumerSecretIssuanceResponse(string Secret, ConsumerSecretMetadataResponse Metadata)
{
    public static ConsumerSecretIssuanceResponse FromResult(ConsumerSecretIssuance issuance) =>
        new(issuance.Secret, ConsumerSecretMetadataResponse.FromResult(issuance.Metadata));
}
