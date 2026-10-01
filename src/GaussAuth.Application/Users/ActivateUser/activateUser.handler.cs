using GaussAuth.Application.Users.Ports;
using GaussAuth.Application.Security;
using GaussAuth.Application.Security.Ports;
using GaussAuth.Domain.Users;
using Microsoft.Extensions.Logging;

namespace GaussAuth.Application.Users.ActivateUser;

public sealed class ActivateUserHandler(IUserRepository userRepository, ISecurityEventRecorder securityEvents, ILogger<ActivateUserHandler> logger)
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
        if (!wasActive) await securityEvents.RecordAsync(SecurityEventType.UserActivated, user.Id, null, null, cancellationToken);

        logger.LogInformation("User {UserId} activation {Outcome}.", id, wasActive ? "unchanged" : "changed");
        return user;
    }
}
