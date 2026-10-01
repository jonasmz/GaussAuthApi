namespace GaussAuth.Application.Passwords.Ports;

public enum PasswordResetOutcome
{
    Succeeded,
    InvalidCredential,
    PasswordPolicyRejected,
    UserNotFound
}
