using GaussAuth.Domain.Users;

namespace GaussAuth.Application.Users.Profiles;

public sealed class UpdateProfileResult
{
    public User? User { get; }

    public bool IsNotFound { get; }

    public IReadOnlyDictionary<string, string[]>? ValidationErrors { get; }

    private UpdateProfileResult(User? user, bool isNotFound, IReadOnlyDictionary<string, string[]>? validationErrors)
    {
        User = user;
        IsNotFound = isNotFound;
        ValidationErrors = validationErrors;
    }

    public static UpdateProfileResult Success(User user) => new(user, false, null);

    public static UpdateProfileResult NotFound() => new(null, true, null);

    public static UpdateProfileResult ValidationFailed(IReadOnlyDictionary<string, string[]> errors) =>
        new(null, false, errors);
}
