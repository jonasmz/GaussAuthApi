namespace GaussAuth.Application.Sessions;

internal enum SessionRejectionReason
{
    NotAuthenticated,
    MalformedCredential,
    CredentialExpired,
    SessionNotFound,
    SessionRevoked,
    SessionExpired,
    InactiveUser,
    InactiveApplication,
    InactiveMembership,
    ApplicationMismatch
}
