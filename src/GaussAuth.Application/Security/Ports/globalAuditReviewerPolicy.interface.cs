namespace GaussAuth.Application.Security.Ports;

public interface IGlobalAuditReviewerPolicy
{
    bool IsGlobalReviewer(Guid userId);
}
