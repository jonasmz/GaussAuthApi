using GaussAuth.Application.Users.Ports;
using GaussAuth.Domain.Users;
using Microsoft.Extensions.Logging;

namespace GaussAuth.Application.Users.GetUser;

public sealed class GetUserHandler(IUserRepository userRepository, ILogger<GetUserHandler> logger)
{
    public async Task<User?> HandleAsync(GetUserQuery query, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByIdAsync(query.Id, cancellationToken);

        logger.LogInformation("User {UserId} retrieval {Outcome}.", query.Id, user is null ? "not found" : "found");

        return user;
    }
}
