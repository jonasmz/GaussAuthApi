namespace GaussAuth.Application.Sessions.Ports;

public interface IAccessCredentialIssuer
{
    string Issue(AccessCredentialClaims claims);
}
