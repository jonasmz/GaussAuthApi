using GaussAuth.Application.Administration.Ports;
using GaussAuth.Application.Applications.Ports;

namespace GaussAuth.Application.Administration.ConsumerCredentials;

public sealed class GetConsumerSecretMetadataHandler(IApplicationRepository applications, IConsumerCredentialStore store)
{
    public async Task<ConsumerSecretOperationResult> HandleAsync(GetConsumerSecretMetadataQuery query, CancellationToken cancellationToken)
    {
        var application = await applications.GetByIdAsync(query.ApplicationId, cancellationToken);
        if (application is null) return ConsumerSecretOperationResult.ApplicationNotFound();
        return ConsumerSecretOperationResult.Described(await store.GetMetadataAsync(application.Id, application.Code, cancellationToken));
    }
}
