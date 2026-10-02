using GaussAuth.Application.Administration.Ports;
using GaussAuth.Application.Applications.Ports;
using GaussAuth.Application.Security;
using GaussAuth.Application.Security.Ports;

namespace GaussAuth.Application.Administration.ConsumerCredentials;

public sealed class RotateConsumerSecretHandler(IApplicationRepository applications, IConsumerCredentialStore store, ISecurityEventRecorder securityEvents)
{
    public async Task<ConsumerSecretOperationResult> HandleAsync(Guid applicationId, CancellationToken cancellationToken)
    {
        var application = await applications.GetByIdAsync(applicationId, cancellationToken);
        if (application is null) return ConsumerSecretOperationResult.ApplicationNotFound();

        await using var transaction = await store.BeginAuditTransactionAsync(cancellationToken);
        var result = await store.RotateAsync(application.Id, application.Code, cancellationToken);
        if (!result.IsSuccess) return result;
        await securityEvents.RecordAsync(SecurityEventType.ConsumerCredentialRotated, null, application.Id, null, "consumer-credential", application.Id, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }
}
