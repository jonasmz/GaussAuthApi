namespace GaussAuth.Application.Users.Ports;

public sealed class CredentialProvisioningResult
{
    public bool Succeeded { get; }

    public bool IsDuplicateEmail { get; }

    public IReadOnlyCollection<string> Errors { get; }

    private CredentialProvisioningResult(bool succeeded, bool isDuplicateEmail, IReadOnlyCollection<string> errors)
    {
        Succeeded = succeeded;
        IsDuplicateEmail = isDuplicateEmail;
        Errors = errors;
    }

    public static CredentialProvisioningResult Success() => new(true, false, Array.Empty<string>());

    public static CredentialProvisioningResult DuplicateEmail() => new(false, true, Array.Empty<string>());

    public static CredentialProvisioningResult Failed(IReadOnlyCollection<string> errors) => new(false, false, errors);
}
