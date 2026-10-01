using GaussAuth.Application.Users.Ports;
using GaussAuth.Domain.Users;
using Microsoft.Extensions.Logging;

namespace GaussAuth.Application.Users.DeactivateUser;

public sealed class DeactivateUserHandler(IUserRepository userRepository, ILogger<DeactivateUserHandler> logger)
{
    public async Task<User?> HandleAsync(Guid id, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByIdAsync(id, cancellationToken);

        if (user is null)
        {
            logger.LogInformation("Deactivation for user {UserId} not found.", id);
            return null;
        }

        var wasActive = user.IsActive;
        user.Deactivate(DateTimeOffset.UtcNow);

        await userRepository.SaveChangesAsync(cancellationToken);

        logger.LogInformation("User {UserId} deactivation {Outcome}.", id, wasActive ? "changed" : "unchanged");
        return user;
    }
}
