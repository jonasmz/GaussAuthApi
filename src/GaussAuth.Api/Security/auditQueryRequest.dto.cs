using Microsoft.AspNetCore.Mvc;

namespace GaussAuth.Api.Security;

public sealed class AuditQueryRequest
{
    public DateTimeOffset? FromUtc { get; init; }
    public DateTimeOffset? ToUtc { get; init; }
    public string? EventType { get; init; }
    public string? Outcome { get; init; }
    public Guid? UserId { get; init; }
    public Guid? ApplicationId { get; init; }
    public Guid? SessionId { get; init; }
    [FromQuery(Name = "pageSize")]
    public int? PageSize { get; init; }
    public string? Cursor { get; init; }
}
