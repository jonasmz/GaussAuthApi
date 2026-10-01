using Microsoft.Extensions.Configuration;

namespace GaussAuth.Infrastructure.Security;

public sealed class SecurityAuditRetentionPolicy
{
    public int? RetentionDays { get; init; }

    public static SecurityAuditRetentionPolicy Load(IConfiguration configuration)
    {
        var value = configuration.GetValue<int?>("SecurityAudit:RetentionDays");
        if (value is <= 0) throw new InvalidOperationException("Security audit retention configuration is invalid.");
        return new SecurityAuditRetentionPolicy { RetentionDays = value };
    }
}
