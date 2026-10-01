namespace GaussAuth.Application.Passwords.Ports;

public interface IPasswordCredentialService
{
    Task<PasswordChangeOutcome> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword, CancellationToken cancellationToken);
    Task<string?> GenerateResetCredentialAsync(Guid userId, CancellationToken cancellationToken);
    Task<PasswordResetOutcome> ResetPasswordAsync(Guid userId, string resetCredential, string newPassword, CancellationToken cancellationToken);
}
