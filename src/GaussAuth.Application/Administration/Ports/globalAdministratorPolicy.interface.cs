namespace GaussAuth.Application.Administration.Ports;

public interface IGlobalAdministratorPolicy
{
    bool IsGlobalAdministrator(Guid userId);
}
