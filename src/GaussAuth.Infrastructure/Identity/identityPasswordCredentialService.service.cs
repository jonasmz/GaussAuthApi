using GaussAuth.Application.Passwords.Ports;
using Microsoft.AspNetCore.Identity;

namespace GaussAuth.Infrastructure.Identity;

public sealed class IdentityPasswordCredentialService(UserManager<IdentityUser<Guid>> userManager) : IPasswordCredentialService
{
    public async Task<PasswordChangeResult> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null) return PasswordChangeResult.Failure(PasswordChangeOutcome.UserNotFound);
        var result = await userManager.ChangePasswordAsync(user, currentPassword, newPassword);
        if (result.Succeeded) return PasswordChangeResult.Success();
        return result.Errors.Any(error => error.Code == "PasswordMismatch")
            ? PasswordChangeResult.Failure(PasswordChangeOutcome.InvalidCurrentPassword)
            : PasswordChangeResult.Failure(PasswordChangeOutcome.PasswordPolicyRejected, result.Errors.Select(error => error.Description).ToArray());
    }

    public async Task<string?> GenerateResetCredentialAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        return user is null ? null : await userManager.GeneratePasswordResetTokenAsync(user);
    }

    public async Task<PasswordResetOutcome> ResetPasswordAsync(Guid userId, string resetCredential, string newPassword, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null) return PasswordResetOutcome.UserNotFound;
        var result = await userManager.ResetPasswordAsync(user, resetCredential, newPassword);
        if (result.Succeeded)
        {
            await userManager.SetLockoutEndDateAsync(user, null);
            return PasswordResetOutcome.Succeeded;
        }
        return result.Errors.Any(error => error.Code == "InvalidToken") ? PasswordResetOutcome.InvalidCredential : PasswordResetOutcome.PasswordPolicyRejected;
    }
}
