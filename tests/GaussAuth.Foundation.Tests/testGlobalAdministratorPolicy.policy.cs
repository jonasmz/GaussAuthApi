using GaussAuth.Application.Administration.Ports;

namespace GaussAuth.Foundation.Tests;

/// <summary>Controllable global-administrator policy so a created user can be made global administrator in a test host.</summary>
internal sealed class TestGlobalAdministratorPolicy : IGlobalAdministratorPolicy
{
    private readonly HashSet<Guid> administrators = [];

    public void Add(Guid userId) { lock (administrators) administrators.Add(userId); }

    public void Remove(Guid userId) { lock (administrators) administrators.Remove(userId); }

    public bool IsGlobalAdministrator(Guid userId) { lock (administrators) return administrators.Contains(userId); }
}
