using GaussAuth.Application.Security;
using GaussAuth.Application.Security.Ports;

namespace GaussAuth.Foundation.Tests;

/// <summary>Drops every event and fails the write of the given type, to prove a state change rolls back with its audit event.</summary>
internal sealed class FailingEventRecorder(SecurityEventType failingType) : ISecurityEventRecorder
{
    public Task RecordAsync(SecurityEventType type, Guid? userId, Guid? applicationId, Guid? sessionId, CancellationToken cancellationToken) =>
        type == failingType ? throw new InvalidOperationException("Forced audit write failure.") : Task.CompletedTask;

    public Task RecordAsync(SecurityEventType type, Guid? userId, Guid? applicationId, Guid? sessionId, string? subjectType, Guid? subjectId, CancellationToken cancellationToken) =>
        RecordAsync(type, userId, applicationId, sessionId, cancellationToken);

    public Task RecordAsync(SecurityEventDraft draft, CancellationToken cancellationToken) => Task.CompletedTask;
}
