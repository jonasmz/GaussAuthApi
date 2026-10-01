using DomainApplication = GaussAuth.Domain.Applications.Application;

namespace GaussAuth.Api.Applications;

public sealed record ApplicationResponse(Guid Id, string Code, string Name, bool IsActive, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt)
{
    public static ApplicationResponse FromDomain(DomainApplication item) => new(item.Id, item.Code, item.Name, item.IsActive, item.CreatedAt, item.UpdatedAt);
}
