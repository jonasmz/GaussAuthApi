namespace GaussAuth.Application.Security.Ports;

public interface ISecurityEventRecorder
{
    Task RecordAsync(SecurityEventType type, Guid? userId, Guid? applicationId, CancellationToken cancellationToken);
}
