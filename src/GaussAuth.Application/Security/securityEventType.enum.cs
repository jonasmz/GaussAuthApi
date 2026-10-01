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
}
