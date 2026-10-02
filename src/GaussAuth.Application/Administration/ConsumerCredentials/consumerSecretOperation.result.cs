namespace GaussAuth.Application.Administration.ConsumerCredentials;

public sealed class ConsumerSecretOperationResult
{
    public ConsumerSecretIssuance? Issuance { get; private init; }
    public ConsumerSecretMetadata? Metadata { get; private init; }
    public string? Failure { get; private init; }
    public bool IsSuccess => Failure is null;

    public static ConsumerSecretOperationResult Issued(ConsumerSecretIssuance issuance) => new() { Issuance = issuance, Metadata = issuance.Metadata };
    public static ConsumerSecretOperationResult Described(ConsumerSecretMetadata metadata) => new() { Metadata = metadata };
    public static ConsumerSecretOperationResult ApplicationNotFound() => new() { Failure = "application-not-found" };
    /// <summary>The operation conflicts with the current state (existing credential, nothing to retire, or a concurrent change).</summary>
    public static ConsumerSecretOperationResult Conflict() => new() { Failure = "conflict" };

    public override string ToString() => $"ConsumerSecretOperationResult {{ Failure = {Failure} }}";
}
