using GaussAuth.Application.Security;
using GaussAuth.Application.Security.Ports;
using Microsoft.Extensions.Logging;

namespace GaussAuth.Infrastructure.Security;

public sealed class LoggingSecurityEventRecorder(ILogger<LoggingSecurityEventRecorder> logger) : ISecurityEventRecorder
{
    public Task RecordAsync(SecurityEventType type, Guid? userId, Guid? applicationId, Guid? sessionId, CancellationToken cancellationToken)
    {
        logger.LogInformation("Security event {EventType} for user {UserId} in application {ApplicationId} session {SessionId}.", type, userId, applicationId, sessionId);
        return Task.CompletedTask;
    }

    public Task RecordAsync(SecurityEventType type, Guid? userId, Guid? applicationId, Guid? sessionId, string? subjectType,
        Guid? subjectId, CancellationToken cancellationToken)
    {
        logger.LogInformation("Security event {EventType} for user {UserId} in application {ApplicationId} session {SessionId} subject {SubjectType} {SubjectId}.", type, userId, applicationId, sessionId, subjectType, subjectId);
        return Task.CompletedTask;
    }
}
