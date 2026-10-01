using GaussAuth.Application.Users.Ports;

namespace GaussAuth.Application.Administration.Users.ListUsers;

public sealed class ListUsersHandler(IUserRepository users, ICredentialProvisioningService credentialProvisioning)
{
    private const int DefaultLimit = 50;
    private const int MaximumLimit = 100;
    private const int MaximumCursorLength = 36;
    private const int MaximumEmailLength = 320;

    public async Task<ListUsersResult> HandleAsync(ListUsersQuery query, CancellationToken cancellationToken)
    {
        var limit = query.Limit ?? DefaultLimit;
        if (limit is < 1 or > MaximumLimit) return ListUsersResult.Invalid();

        Guid? afterId = null;
        if (query.Cursor is not null)
        {
            if (query.Cursor.Length > MaximumCursorLength || !Guid.TryParse(query.Cursor, out var cursor)) return ListUsersResult.Invalid();
            afterId = cursor;
        }

        string? normalizedEmail = null;
        if (query.Email is not null)
        {
            var email = query.Email.Trim();
            if (email.Length is 0 or > MaximumEmailLength) return ListUsersResult.Invalid();
            normalizedEmail = credentialProvisioning.NormalizeEmail(email);
        }

        var items = await users.ListAsync(query.IsActive, normalizedEmail, afterId, limit, cancellationToken);
        return ListUsersResult.Success(items, items.Count == limit ? items[^1].Id.ToString() : null);
    }
}
