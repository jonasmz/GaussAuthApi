namespace GaussAuth.Application.Administration.Authorization;

public sealed class AdministrativeAuthorization
{
    public AdministrativeAuthorizationOutcome Outcome { get; private init; }
    public Guid? ActorUserId { get; private init; }
    public bool IsGlobal { get; private init; }

    public bool IsAuthorized => Outcome == AdministrativeAuthorizationOutcome.Authorized;

    public static AdministrativeAuthorization Authorized(Guid actorUserId, bool isGlobal) =>
        new() { Outcome = AdministrativeAuthorizationOutcome.Authorized, ActorUserId = actorUserId, IsGlobal = isGlobal };

    public static AdministrativeAuthorization Unauthenticated() => new() { Outcome = AdministrativeAuthorizationOutcome.Unauthenticated };

    public static AdministrativeAuthorization Forbidden(Guid actorUserId) =>
        new() { Outcome = AdministrativeAuthorizationOutcome.Forbidden, ActorUserId = actorUserId };
}
