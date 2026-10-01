namespace GaussAuth.Application.Security.Ports;

public interface ISecurityEventRecorder
{
    Task RecordAsync(SecurityEventType type, Guid? userId, Guid? applicationId, Guid? sessionId, CancellationToken cancellationToken);
    Task RecordAsync(SecurityEventDraft draft, CancellationToken cancellationToken) =>
        throw new NotSupportedException("This recorder does not support security event drafts.");
}
