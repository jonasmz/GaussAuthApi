namespace GaussAuth.Application.Login;

internal enum LoginFailureReason
{
    UnknownEmail,
    InactiveUser,
    UnknownApplication,
    InactiveApplication,
    MissingMembership,
    InactiveMembership,
    InvalidPassword,
    LockedOut
}
