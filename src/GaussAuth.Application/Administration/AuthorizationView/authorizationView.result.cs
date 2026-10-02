namespace GaussAuth.Application.Administration.AuthorizationView;

public sealed class AuthorizationView
{
    public Guid ApplicationId { get; init; }
    public string ApplicationCode { get; init; } = string.Empty;
    public bool ApplicationIsActive { get; init; }
    public Guid UserId { get; init; }
    public bool UserIsActive { get; init; }
    /// <summary>Whether the membership is active, or <see langword="null"/> when the user has no membership.</summary>
    public bool? MembershipIsActive { get; init; }
    public IReadOnlyList<(Guid Id, string Name)> Roles { get; init; } = [];
    public IReadOnlyList<string> Permissions { get; init; } = [];
}
