using GaussAuth.Application.Security;
using GaussAuth.Application.Security.Ports;
using Microsoft.Extensions.Logging;

namespace GaussAuth.Infrastructure.Security;

public sealed class LoggingSecurityEventRecorder(ILogger<LoggingSecurityEventRecorder> logger) : ISecurityEventRecorder
{
    public Task RecordAsync(SecurityEventType type, Guid? userId, Guid? applicationId, CancellationToken cancellationToken)
    {
        logger.LogInformation("Security event {EventType} for user {UserId} in application {ApplicationId}.", type, userId, applicationId);
        return Task.CompletedTask;
    }
}
