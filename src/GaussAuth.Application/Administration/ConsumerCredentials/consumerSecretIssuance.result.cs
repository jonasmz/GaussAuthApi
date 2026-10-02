namespace GaussAuth.Application.Administration.ConsumerCredentials;

/// <summary>A newly issued plaintext secret, returned once. <see cref="ToString"/> redacts it so it cannot be logged by accident.</summary>
public sealed record ConsumerSecretIssuance(string Secret, ConsumerSecretMetadata Metadata)
{
    public override string ToString() => "ConsumerSecretIssuance { Secret = [redacted] }";
}
