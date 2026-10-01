namespace GaussAuth.Application.Sessions.Ports;

public interface IAccessCredentialKeySet
{
    IReadOnlyList<PublicSigningKey> GetPublicKeys();
}
