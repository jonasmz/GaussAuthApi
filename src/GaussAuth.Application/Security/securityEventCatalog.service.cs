namespace GaussAuth.Application.Security;

public sealed class SecurityEventCatalog
{
    public SecurityEventDefinition Get(SecurityEventType type) => type switch
    {
        SecurityEventType.LoginSucceeded => Operational("authentication.login.succeeded", SecurityEventOutcome.Succeeded),
        SecurityEventType.LoginFailed => Operational("authentication.login.failed", SecurityEventOutcome.Rejected),
        SecurityEventType.AccountLockedOut => Operational("authentication.lockout", SecurityEventOutcome.Rejected),
        SecurityEventType.SessionCreated => Operational("session.created", SecurityEventOutcome.Succeeded),
        SecurityEventType.AccessRenewed => Operational("session.access.renewed", SecurityEventOutcome.Succeeded),
        SecurityEventType.SessionRevoked => Critical("session.revoked", SecurityEventOutcome.Succeeded),
        SecurityEventType.LogoutCompleted => Operational("session.logout", SecurityEventOutcome.Succeeded),
        SecurityEventType.AccessRejectedExpired => Operational("session.access.rejected.expired", SecurityEventOutcome.Rejected),
        SecurityEventType.AccessRejectedRevoked => Operational("session.access.rejected.revoked", SecurityEventOutcome.Rejected),
        SecurityEventType.AccessRejectedInvalidState => Operational("session.access.rejected.state", SecurityEventOutcome.Rejected),
        SecurityEventType.AccessRejectedApplicationMismatch => Operational("session.access.rejected.application-mismatch", SecurityEventOutcome.Rejected),
        SecurityEventType.PasswordChanged => Critical("password.changed", SecurityEventOutcome.Succeeded),
        SecurityEventType.PasswordRecoveryRequested => Operational("password.recovery.requested", SecurityEventOutcome.Succeeded),
        SecurityEventType.PasswordReset => Critical("password.reset.succeeded", SecurityEventOutcome.Succeeded),
        SecurityEventType.PasswordResetFailed => Operational("password.reset.failed", SecurityEventOutcome.Rejected),
        SecurityEventType.AuthorizationContextResolved => Operational("authorization.context.resolved", SecurityEventOutcome.Succeeded),
        SecurityEventType.AuthorizationContextRejected => Operational("authorization.context.rejected", SecurityEventOutcome.Rejected),
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };

    private static SecurityEventDefinition Operational(string type, SecurityEventOutcome outcome) => new(type, outcome, SecurityEventReliability.Operational);
    private static SecurityEventDefinition Critical(string type, SecurityEventOutcome outcome) => new(type, outcome, SecurityEventReliability.Critical);
}
