namespace GaussAuth.Application.Administration.Users.ListUsers;

public sealed record ListUsersQuery(bool? IsActive, string? Email, string? Cursor, int? Limit);
