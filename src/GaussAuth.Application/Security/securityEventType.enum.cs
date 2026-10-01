namespace GaussAuth.Application.Security;

public enum SecurityEventType
{
    LoginSucceeded,
    LoginFailed,
    AccountLockedOut,
    SessionCreated,
    AccessRenewed,
    SessionRevoked,
    LogoutCompleted,
    AccessRejectedExpired,
    AccessRejectedRevoked,
    AccessRejectedInvalidState,
    AccessRejectedApplicationMismatch,
    PasswordChanged,
    PasswordRecoveryRequested,
    PasswordReset,
    PasswordResetFailed,
    AuthorizationContextResolved,
    AuthorizationContextRejected
    ,UserActivated, UserDeactivated, ApplicationActivated, ApplicationDeactivated,
    MembershipCreated, MembershipActivated, MembershipDeactivated,
    RoleCreated, RoleActivated, RoleDeactivated, PermissionCreated, PermissionActivated, PermissionDeactivated,
    RoleAssigned, RoleRemoved, PermissionAssigned, PermissionRemoved, ConsumerAuthenticationFailed,
    AvatarUpdated, AvatarRemoved, AvatarUploadRejected
}
