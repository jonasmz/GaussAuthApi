namespace GaussAuth.Application.Login;

public sealed class LoginOperationResult
{
    public Guid? UserId { get; private init; }
    public Guid? ApplicationId { get; private init; }
    public bool IsSuccess { get; private init; }
    internal LoginFailureReason? Reason { get; private init; }

    internal static LoginOperationResult Success(Guid userId, Guid applicationId) =>
        new() { IsSuccess = true, UserId = userId, ApplicationId = applicationId };

    internal static LoginOperationResult Failure(LoginFailureReason reason, Guid? userId = null, Guid? applicationId = null) =>
        new() { IsSuccess = false, Reason = reason, UserId = userId, ApplicationId = applicationId };
}
