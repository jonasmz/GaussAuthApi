using GaussAuth.Infrastructure.Configuration;
using Microsoft.Extensions.Configuration;

namespace GaussAuth.Infrastructure.Security;

public sealed class SecurityAuditRetentionPolicy
{
    public int? RetentionDays { get; init; }

    public static SecurityAuditRetentionPolicy Load(IConfiguration configuration)
    {
        var value = configuration.GetOptionalBoundedInt32("SecurityAudit:RetentionDays", 1, int.MaxValue);
        return new SecurityAuditRetentionPolicy { RetentionDays = value };
    }
}
