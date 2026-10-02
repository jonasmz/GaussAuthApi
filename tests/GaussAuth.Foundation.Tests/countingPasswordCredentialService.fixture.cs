using System.Runtime.CompilerServices;
using GaussAuth.Application.Passwords.Ports;

namespace GaussAuth.Foundation.Tests;

/// <summary>
/// Wraps the real credential service and counts reset-credential generation so a test can prove that none is created
/// when no delivery channel exists.
/// </summary>
internal sealed class CountingPasswordCredentialService(IPasswordCredentialService inner, StrongBox<int> generated) : IPasswordCredentialService
{
    public Task<PasswordChangeResult> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword, CancellationToken cancellationToken) =>
        inner.ChangePasswordAsync(userId, currentPassword, newPassword, cancellationToken);

    public Task<string?> GenerateResetCredentialAsync(Guid userId, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref generated.Value);
        return inner.GenerateResetCredentialAsync(userId, cancellationToken);
    }

    public Task<PasswordResetOutcome> ResetPasswordAsync(Guid userId, string resetCredential, string newPassword, CancellationToken cancellationToken) =>
        inner.ResetPasswordAsync(userId, resetCredential, newPassword, cancellationToken);
}
