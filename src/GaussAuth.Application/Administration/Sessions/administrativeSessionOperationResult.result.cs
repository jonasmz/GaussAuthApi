namespace GaussAuth.Application.Administration.Sessions;

public sealed class AdministrativeSessionOperationResult
{
    public SessionSummary? Session { get; private init; }
    public bool IsNotFound => Session is null;

    public static AdministrativeSessionOperationResult Success(SessionSummary session) => new() { Session = session };
    public static AdministrativeSessionOperationResult NotFound() => new();
}
