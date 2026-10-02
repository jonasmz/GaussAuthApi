namespace GaussAuth.Infrastructure.Bootstrap;

public sealed record FirstAdministratorBootstrapInput(
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string DisplayName);

/// <summary>Value-free bootstrap outcome. Validation messages name rules but never include supplied values.</summary>
public sealed record FirstAdministratorBootstrapResult(
    bool Succeeded,
    Guid? UserId,
    string Category,
    IReadOnlyDictionary<string, string[]>? ValidationErrors = null)
{
    public static FirstAdministratorBootstrapResult Success(Guid userId) => new(true, userId, "success");
    public static FirstAdministratorBootstrapResult UsersExist() => new(false, null, "users-exist");
    public static FirstAdministratorBootstrapResult InvalidEmail() => new(false, null, "invalid-email");
    public static FirstAdministratorBootstrapResult PasswordPolicy(IReadOnlyDictionary<string, string[]>? errors) =>
        new(false, null, "password-policy", errors);
}
