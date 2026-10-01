namespace GaussAuth.Application.Login.Ports;

public enum CredentialVerificationOutcome
{
    Success,
    InvalidPassword,
    LockedOut
}
