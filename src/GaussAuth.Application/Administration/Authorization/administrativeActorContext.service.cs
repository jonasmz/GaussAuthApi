using GaussAuth.Application.Administration.Ports;

namespace GaussAuth.Application.Administration.Authorization;

public sealed class AdministrativeActorContext : IAdministrativeActorContext
{
    public Guid? ActorUserId { get; private set; }

    public void Set(Guid actorUserId)
    {
        if (actorUserId == Guid.Empty) throw new ArgumentException("Actor identifier must be present.", nameof(actorUserId));
        ActorUserId = actorUserId;
    }
}
