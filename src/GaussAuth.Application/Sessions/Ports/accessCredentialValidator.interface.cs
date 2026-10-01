namespace GaussAuth.Application.Sessions.Ports;

public interface IAccessCredentialValidator
{
    /// <summary>
    /// Verifies authenticity only. Returns <see langword="null"/> when the credential is unauthentic or malformed.
    /// It does not judge lifetime; the caller compares expiry against server time.
    /// </summary>
    Task<AccessCredentialClaims?> ValidateAsync(string credential, CancellationToken cancellationToken);
}
