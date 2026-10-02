using GaussAuth.Application.Users.CreateUser;
using GaussAuth.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GaussAuth.Infrastructure.Bootstrap;

/// <summary>
/// Creates the first local user only while the user store is empty.  Authority is deliberately not assigned here:
/// operators must explicitly configure the resulting identifier as a global administrator after this command exits.
/// </summary>
public sealed class FirstAdministratorBootstrap(AuthenticationDbContext context, CreateUserHandler createUser)
{
    public async Task<FirstAdministratorBootstrapResult> RunAsync(
        FirstAdministratorBootstrapInput input,
        CancellationToken cancellationToken)
    {
        if (await context.DomainUsers.AnyAsync(cancellationToken)) return FirstAdministratorBootstrapResult.UsersExist();

        try
        {
            var created = await createUser.HandleAsync(new CreateUserCommand(
                input.Email,
                input.Password,
                input.FirstName,
                input.LastName,
                input.DisplayName,
                null), cancellationToken);

            if (created.User is not null) return FirstAdministratorBootstrapResult.Success(created.User.Id);
            if (created.IsDuplicateEmail) return FirstAdministratorBootstrapResult.UsersExist();
            return HasEmailError(created.ValidationErrors)
                ? FirstAdministratorBootstrapResult.InvalidEmail()
                : FirstAdministratorBootstrapResult.PasswordPolicy(created.ValidationErrors);
        }
        catch (ArgumentException)
        {
            return FirstAdministratorBootstrapResult.InvalidEmail();
        }
    }

    private static bool HasEmailError(IReadOnlyDictionary<string, string[]>? errors) =>
        errors is not null && errors.Any(item => item.Key.Contains("email", StringComparison.OrdinalIgnoreCase) ||
            item.Value.Any(value => value.Contains("email", StringComparison.OrdinalIgnoreCase)));
}
