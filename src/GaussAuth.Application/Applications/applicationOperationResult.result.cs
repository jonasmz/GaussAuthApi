using DomainApplication = GaussAuth.Domain.Applications.Application;

namespace GaussAuth.Application.Applications;

public sealed class ApplicationOperationResult
{
    public DomainApplication? Application { get; private init; }
    public bool IsDuplicate { get; private init; }
    public bool IsInvalid { get; private init; }
    public static ApplicationOperationResult Success(DomainApplication application) => new() { Application = application };
    public static ApplicationOperationResult Duplicate() => new() { IsDuplicate = true };
    public static ApplicationOperationResult Invalid() => new() { IsInvalid = true };
}
