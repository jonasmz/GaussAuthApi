namespace GaussAuth.Application.Sessions.Ports;

public interface ISessionRevoker
{
    Task<int> RevokeAllForUserAsync(Guid userId, CancellationToken cancellationToken);
}
