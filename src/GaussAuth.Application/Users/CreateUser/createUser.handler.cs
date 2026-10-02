using GaussAuth.Application.Security;
using GaussAuth.Application.Security.Ports;
using GaussAuth.Application.Users.Ports;
using GaussAuth.Domain.Users;
using Microsoft.Extensions.Logging;

namespace GaussAuth.Application.Users.CreateUser;

public sealed class CreateUserHandler(
    IUserRepository userRepository,
    ICredentialProvisioningService credentialProvisioningService,
    ISecurityEventRecorder securityEvents,
    ILogger<CreateUserHandler> logger)
{
    public async Task<CreateUserResult> HandleAsync(CreateUserCommand command, CancellationToken cancellationToken)
    {
        var email = command.Email.Trim();
        var normalizedEmail = credentialProvisioningService.NormalizeEmail(email);

        if (await userRepository.ExistsByNormalizedEmailAsync(normalizedEmail, cancellationToken))
        {
            logger.LogInformation("User creation rejected: duplicate normalized email.");
            return CreateUserResult.DuplicateEmail();
        }

        var userId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        await using var transaction = await userRepository.BeginTransactionAsync(cancellationToken);

        var credentialResult = await credentialProvisioningService.CreateCredentialAsync(
            userId, email, command.Password, cancellationToken);

        if (credentialResult.IsDuplicateEmail)
        {
            await transaction.RollbackAsync(cancellationToken);
            logger.LogInformation("User creation rejected: duplicate normalized email detected during credential provisioning.");
            return CreateUserResult.DuplicateEmail();
        }

        if (!credentialResult.Succeeded)
        {
            await transaction.RollbackAsync(cancellationToken);
            logger.LogInformation("User creation rejected: credential provisioning failed.");
            return CreateUserResult.ValidationFailed(
                new Dictionary<string, string[]> { ["password"] = credentialResult.Errors.ToArray() });
        }

        var profile = UserProfile.Create(
            userId,
            command.FirstName,
            command.LastName,
            command.DisplayName,
            command.PhoneNumber,
            now);

        var user = User.Create(userId, email, normalizedEmail, profile, now);

        await userRepository.AddAsync(user, cancellationToken);

        if (!await userRepository.TrySaveChangesAsync(cancellationToken))
        {
            await transaction.RollbackAsync(cancellationToken);
            logger.LogInformation("User creation rejected: duplicate normalized email detected at persistence.");
            return CreateUserResult.DuplicateEmail();
        }

        await transaction.CommitAsync(cancellationToken);
        await securityEvents.RecordAsync(SecurityEventType.UserCreated, userId, null, null, "user", userId, cancellationToken);
        logger.LogInformation("User {UserId} created.", userId);
        return CreateUserResult.Success(user);
    }
}
