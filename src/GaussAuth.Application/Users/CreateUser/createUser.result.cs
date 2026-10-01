using GaussAuth.Domain.Users;

namespace GaussAuth.Application.Users.CreateUser;

public sealed class CreateUserResult
{
    public User? User { get; }

    public bool IsDuplicateEmail { get; }

    public IReadOnlyDictionary<string, string[]>? ValidationErrors { get; }

    private CreateUserResult(User? user, bool isDuplicateEmail, IReadOnlyDictionary<string, string[]>? validationErrors)
    {
        User = user;
        IsDuplicateEmail = isDuplicateEmail;
        ValidationErrors = validationErrors;
    }

    public static CreateUserResult Success(User user) => new(user, false, null);

    public static CreateUserResult DuplicateEmail() => new(null, true, null);

    public static CreateUserResult ValidationFailed(IReadOnlyDictionary<string, string[]> errors) =>
        new(null, false, errors);
}
