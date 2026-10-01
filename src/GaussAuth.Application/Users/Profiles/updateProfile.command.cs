namespace GaussAuth.Application.Users.Profiles;

public sealed record UpdateProfileCommand(
    Guid Id,
    string FirstName,
    string LastName,
    string DisplayName,
    string? PhoneNumber,
    string? AvatarReference);
