using GaussAuth.Application.Users.Ports;
using GaussAuth.Domain.Users;
using Microsoft.Extensions.Logging;

namespace GaussAuth.Application.Users.ActivateUser;

public sealed class ActivateUserHandler(IUserRepository userRepository, ILogger<ActivateUserHandler> logger)
{
    public async Task<User?> HandleAsync(Guid id, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByIdAsync(id, cancellationToken);

        if (user is null)
        {
            logger.LogInformation("Activation for user {UserId} not found.", id);
            return null;
        }

        var wasActive = user.IsActive;
        user.Activate(DateTimeOffset.UtcNow);

        await userRepository.SaveChangesAsync(cancellationToken);

        logger.LogInformation("User {UserId} activation {Outcome}.", id, wasActive ? "unchanged" : "changed");
        return user;
    }
}
