using GaussAuth.Application.Administration.ConsumerCredentials;
using GaussAuth.Application.Security.Ports;

namespace GaussAuth.Application.Administration.Ports;

/// <summary>
/// Persistence and secret generation for managed consumer credentials. Changing operations save their change but must be
/// run inside the transaction from <see cref="BeginAuditTransactionAsync"/> together with the critical audit event.
/// A concurrent change is reported as a conflict result, never as lost data.
/// </summary>
public interface IConsumerCredentialStore
{
    Task<ISecurityAuditTransaction> BeginAuditTransactionAsync(CancellationToken cancellationToken);

    /// <summary>Creates the first managed credential; conflicts when a managed or configured credential already exists.</summary>
    Task<ConsumerSecretOperationResult> GenerateAsync(Guid applicationId, string applicationCode, CancellationToken cancellationToken);

    /// <summary>Rotates the credential, importing a configured current hash as retiring when there is no managed record.</summary>
    Task<ConsumerSecretOperationResult> RotateAsync(Guid applicationId, string applicationCode, CancellationToken cancellationToken);

    /// <summary>Clears the retiring hash of a managed credential; conflicts when there is no managed credential.</summary>
    Task<ConsumerSecretOperationResult> RetirePreviousAsync(Guid applicationId, string applicationCode, CancellationToken cancellationToken);

    Task<ConsumerSecretMetadata> GetMetadataAsync(Guid applicationId, string applicationCode, CancellationToken cancellationToken);
}
