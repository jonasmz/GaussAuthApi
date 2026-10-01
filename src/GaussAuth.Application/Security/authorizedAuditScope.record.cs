namespace GaussAuth.Application.Security;

public sealed record AuthorizedAuditScope(AuditQueryScope Scope, Guid? ApplicationId)
{
    public static AuthorizedAuditScope Application(Guid applicationId) => new(AuditQueryScope.Application, applicationId);
    public static AuthorizedAuditScope Global() => new(AuditQueryScope.Global, null);
}
