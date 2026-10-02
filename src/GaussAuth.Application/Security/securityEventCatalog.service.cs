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
        SecurityEventType.ConsumerAuthenticationFailed => Operational("consumer.authentication.failed", SecurityEventOutcome.Rejected),
        SecurityEventType.UserActivated => Critical("user.activated", SecurityEventOutcome.Succeeded),
        SecurityEventType.UserDeactivated => Critical("user.deactivated", SecurityEventOutcome.Succeeded),
        SecurityEventType.ApplicationActivated => Critical("application.activated", SecurityEventOutcome.Succeeded),
        SecurityEventType.ApplicationDeactivated => Critical("application.deactivated", SecurityEventOutcome.Succeeded),
        SecurityEventType.MembershipCreated => Critical("membership.created", SecurityEventOutcome.Succeeded),
        SecurityEventType.MembershipActivated => Critical("membership.activated", SecurityEventOutcome.Succeeded),
        SecurityEventType.MembershipDeactivated => Critical("membership.deactivated", SecurityEventOutcome.Succeeded),
        SecurityEventType.RoleCreated => Critical("role.created", SecurityEventOutcome.Succeeded),
        SecurityEventType.RoleActivated => Critical("role.activated", SecurityEventOutcome.Succeeded),
        SecurityEventType.RoleDeactivated => Critical("role.deactivated", SecurityEventOutcome.Succeeded),
        SecurityEventType.PermissionCreated => Critical("permission.created", SecurityEventOutcome.Succeeded),
        SecurityEventType.PermissionActivated => Critical("permission.activated", SecurityEventOutcome.Succeeded),
        SecurityEventType.PermissionDeactivated => Critical("permission.deactivated", SecurityEventOutcome.Succeeded),
        SecurityEventType.RoleAssigned => Critical("role.assigned", SecurityEventOutcome.Succeeded),
        SecurityEventType.RoleRemoved => Critical("role.removed", SecurityEventOutcome.Succeeded),
        SecurityEventType.PermissionAssigned => Critical("permission.assigned", SecurityEventOutcome.Succeeded),
        SecurityEventType.PermissionRemoved => Critical("permission.removed", SecurityEventOutcome.Succeeded),
        SecurityEventType.AvatarUpdated => Operational("profile.avatar.updated", SecurityEventOutcome.Succeeded),
        SecurityEventType.AvatarRemoved => Operational("profile.avatar.removed", SecurityEventOutcome.Succeeded),
        SecurityEventType.AvatarUploadRejected => Operational("profile.avatar.upload.rejected", SecurityEventOutcome.Rejected),
        SecurityEventType.ApplicationRegistered => Critical("application.registered", SecurityEventOutcome.Succeeded),
        SecurityEventType.UserProfileUpdated => Critical("user.profile.updated", SecurityEventOutcome.Succeeded),
        SecurityEventType.UserCreated => Critical("user.created", SecurityEventOutcome.Succeeded),
        SecurityEventType.RoleUpdated => Critical("role.updated", SecurityEventOutcome.Succeeded),
        SecurityEventType.PermissionUpdated => Critical("permission.updated", SecurityEventOutcome.Succeeded),
        SecurityEventType.ConsumerCredentialGenerated => Critical("consumer.credential.generated", SecurityEventOutcome.Succeeded),
        SecurityEventType.ConsumerCredentialRotated => Critical("consumer.credential.rotated", SecurityEventOutcome.Succeeded),
        SecurityEventType.ConsumerCredentialPreviousRetired => Critical("consumer.credential.previous-retired", SecurityEventOutcome.Succeeded),
        SecurityEventType.AdministrativeAccessDenied => Operational("administration.access.denied", SecurityEventOutcome.Rejected),
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };

    private static SecurityEventDefinition Operational(string type, SecurityEventOutcome outcome) => new(type, outcome, SecurityEventReliability.Operational);
    private static SecurityEventDefinition Critical(string type, SecurityEventOutcome outcome) => new(type, outcome, SecurityEventReliability.Critical);
}
