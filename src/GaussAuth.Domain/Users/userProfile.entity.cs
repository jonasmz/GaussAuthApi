namespace GaussAuth.Domain.Users;

public sealed class UserProfile
{
    public Guid UserId { get; private set; }

    public string FirstName { get; private set; }

    public string LastName { get; private set; }

    public string DisplayName { get; private set; }

    public string? PhoneNumber { get; private set; }

    public string? AvatarReference { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    private UserProfile(
        Guid userId,
        string firstName,
        string lastName,
        string displayName,
        string? phoneNumber,
        string? avatarReference,
        DateTimeOffset now)
    {
        UserId = userId;
        FirstName = firstName;
        LastName = lastName;
        DisplayName = displayName;
        PhoneNumber = phoneNumber;
        AvatarReference = avatarReference;
        CreatedAt = now;
        UpdatedAt = now;
    }

#pragma warning disable CS8618
    private UserProfile()
    {
    }
#pragma warning restore CS8618

    public static UserProfile Create(
        Guid userId,
        string firstName,
        string lastName,
        string displayName,
        string? phoneNumber,
        string? avatarReference,
        DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(firstName))
        {
            throw new ArgumentException("First name must not be blank.", nameof(firstName));
        }

        if (string.IsNullOrWhiteSpace(lastName))
        {
            throw new ArgumentException("Last name must not be blank.", nameof(lastName));
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ArgumentException("Display name must not be blank.", nameof(displayName));
        }

        return new UserProfile(userId, firstName, lastName, displayName, phoneNumber, avatarReference, now);
    }

    public void Update(
        string firstName,
        string lastName,
        string displayName,
        string? phoneNumber,
        string? avatarReference,
        DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(firstName))
        {
            throw new ArgumentException("First name must not be blank.", nameof(firstName));
        }

        if (string.IsNullOrWhiteSpace(lastName))
        {
            throw new ArgumentException("Last name must not be blank.", nameof(lastName));
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ArgumentException("Display name must not be blank.", nameof(displayName));
        }

        FirstName = firstName;
        LastName = lastName;
        DisplayName = displayName;
        PhoneNumber = phoneNumber;
        AvatarReference = avatarReference;
        UpdatedAt = now;
    }
}
