using GaussAuth.Application.Users.Ports;
using Microsoft.Extensions.Logging;

namespace GaussAuth.Application.Users.Profiles;

public sealed class UpdateProfileHandler(IUserRepository userRepository, ILogger<UpdateProfileHandler> logger)
{
    public async Task<UpdateProfileResult> HandleAsync(UpdateProfileCommand command, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByIdAsync(command.Id, cancellationToken);

        if (user is null)
        {
            logger.LogInformation("Profile update for user {UserId} not found.", command.Id);
            return UpdateProfileResult.NotFound();
        }

        var now = DateTimeOffset.UtcNow;
        user.UpdateProfile(
            command.FirstName,
            command.LastName,
            command.DisplayName,
            command.PhoneNumber,
            command.AvatarReference,
            now);

        await userRepository.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Profile update for user {UserId} updated.", command.Id);
        return UpdateProfileResult.Success(user);
    }
}
