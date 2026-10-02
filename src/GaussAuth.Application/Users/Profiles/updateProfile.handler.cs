using GaussAuth.Application.Security;
using GaussAuth.Application.Security.Ports;
using GaussAuth.Application.Users.Ports;
using Microsoft.Extensions.Logging;

namespace GaussAuth.Application.Users.Profiles;

public sealed class UpdateProfileHandler(IUserRepository userRepository, ISecurityEventRecorder securityEvents, ILogger<UpdateProfileHandler> logger)
{
    public async Task<UpdateProfileResult> HandleAsync(UpdateProfileCommand command, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByIdAsync(command.Id, cancellationToken);

        if (user is null)
        {
            logger.LogInformation("Profile update for user {UserId} not found.", command.Id);
            return UpdateProfileResult.NotFound();
        }

        var before = (user.Profile.FirstName, user.Profile.LastName, user.Profile.DisplayName, user.Profile.PhoneNumber);
        var now = DateTimeOffset.UtcNow;
        user.UpdateProfile(
            command.FirstName,
            command.LastName,
            command.DisplayName,
            command.PhoneNumber,
            now);

        await userRepository.SaveChangesAsync(cancellationToken);

        // Only a real change is audited, and only identifiers are recorded: no personal data.
        if (before != (user.Profile.FirstName, user.Profile.LastName, user.Profile.DisplayName, user.Profile.PhoneNumber))
            await securityEvents.RecordAsync(SecurityEventType.UserProfileUpdated, user.Id, null, null, cancellationToken);

        logger.LogInformation("Profile update for user {UserId} updated.", command.Id);
        return UpdateProfileResult.Success(user);
    }
}
