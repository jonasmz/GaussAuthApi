namespace GaussAuth.Application.Administration.Ports;

/// <summary>
/// The authenticated administrator performing the current request, set only after authorization succeeds.
/// Used to stamp the actor on security events without changing every service signature.
/// </summary>
public interface IAdministrativeActorContext
{
    Guid? ActorUserId { get; }
}
