namespace GaussAuth.Application.Passwords.Ports;

public enum PasswordChangeOutcome
{
    Succeeded,
    InvalidCurrentPassword,
    PasswordPolicyRejected,
    UserNotFound
}
