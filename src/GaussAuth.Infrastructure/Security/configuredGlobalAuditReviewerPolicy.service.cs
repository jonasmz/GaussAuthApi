using GaussAuth.Application.Security.Ports;
using Microsoft.Extensions.Configuration;

namespace GaussAuth.Infrastructure.Security;

public sealed class ConfiguredGlobalAuditReviewerPolicy : IGlobalAuditReviewerPolicy
{
    private readonly Guid? globalReviewerUserId;

    public ConfiguredGlobalAuditReviewerPolicy(IConfiguration configuration)
    {
        var configured = configuration["SecurityAudit:GlobalReviewerUserId"];
        if (!string.IsNullOrWhiteSpace(configured) && !Guid.TryParse(configured, out var userId))
            throw new InvalidOperationException("Security audit reviewer configuration is invalid.");
        globalReviewerUserId = string.IsNullOrWhiteSpace(configured) ? null : Guid.Parse(configured);
    }

    public bool IsGlobalReviewer(Guid userId) => globalReviewerUserId == userId;
}
