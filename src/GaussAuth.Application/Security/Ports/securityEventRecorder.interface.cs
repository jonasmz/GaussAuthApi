namespace GaussAuth.Application.Security.Ports;

public interface ISecurityEventRecorder
{
    Task RecordAsync(SecurityEventType type, Guid? userId, Guid? applicationId, Guid? sessionId, CancellationToken cancellationToken);

    /// <summary>
    /// Records an event identifying the affected record through a typed subject (for example <c>role</c>, <c>permission</c>,
    /// <c>membership</c>, <c>role-permission</c>, <c>user-role</c>, <c>session</c>, <c>application</c>, or <c>consumer-credential</c>).
    /// Recorders that do not persist subjects fall back to the event without a subject.
    /// </summary>
    Task RecordAsync(SecurityEventType type, Guid? userId, Guid? applicationId, Guid? sessionId, string? subjectType, Guid? subjectId, CancellationToken cancellationToken) =>
        RecordAsync(type, userId, applicationId, sessionId, cancellationToken);

    Task RecordAsync(SecurityEventDraft draft, CancellationToken cancellationToken) =>
        throw new NotSupportedException("This recorder does not support security event drafts.");
}
