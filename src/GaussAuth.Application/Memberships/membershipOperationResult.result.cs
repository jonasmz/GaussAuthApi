using GaussAuth.Domain.Memberships;

namespace GaussAuth.Application.Memberships;

public sealed class MembershipOperationResult
{
    public ApplicationMembership? Membership { get; private init; }
    public string? Failure { get; private init; }
    public static MembershipOperationResult Success(ApplicationMembership value) => new() { Membership = value };
    public static MembershipOperationResult Invalid() => new() { Failure = "invalid" };
    public static MembershipOperationResult UserNotFound() => new() { Failure = "user-not-found" };
    public static MembershipOperationResult ApplicationNotFound() => new() { Failure = "application-not-found" };
    public static MembershipOperationResult MembershipNotFound() => new() { Failure = "membership-not-found" };
    public static MembershipOperationResult InactiveApplication() => new() { Failure = "inactive-application" };
    public static MembershipOperationResult InactiveUser() => new() { Failure = "inactive-user" };
    public static MembershipOperationResult Duplicate() => new() { Failure = "duplicate" };
}
