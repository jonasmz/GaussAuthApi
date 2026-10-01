namespace GaussAuth.Application.Users.CreateUser;

public sealed record CreateUserCommand(
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string DisplayName,
    string? PhoneNumber,
    string? AvatarReference);
