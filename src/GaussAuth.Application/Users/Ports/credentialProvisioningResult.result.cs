namespace GaussAuth.Application.Users.Ports;

public sealed class CredentialProvisioningResult
{
    public bool Succeeded { get; }

    public IReadOnlyCollection<string> Errors { get; }

    private CredentialProvisioningResult(bool succeeded, IReadOnlyCollection<string> errors)
    {
        Succeeded = succeeded;
        Errors = errors;
    }

    public static CredentialProvisioningResult Success() => new(true, Array.Empty<string>());

    public static CredentialProvisioningResult Failed(IReadOnlyCollection<string> errors) => new(false, errors);
}
