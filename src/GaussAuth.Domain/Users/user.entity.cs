namespace GaussAuth.Domain.Users;

public sealed class User
{
    public Guid Id { get; private set; }

    public string Email { get; private set; }

    public string NormalizedEmail { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public UserProfile Profile { get; private set; }

    private User(Guid id, string email, string normalizedEmail, UserProfile profile, DateTimeOffset now)
    {
        Id = id;
        Email = email;
        NormalizedEmail = normalizedEmail;
        IsActive = true;
        CreatedAt = now;
        UpdatedAt = now;
        Profile = profile;
    }

#pragma warning disable CS8618
    private User()
    {
    }
#pragma warning restore CS8618

    public static User Create(Guid id, string email, string normalizedEmail, UserProfile profile, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ArgumentException("Email must not be blank.", nameof(email));
        }

        if (string.IsNullOrWhiteSpace(normalizedEmail))
        {
            throw new ArgumentException("Normalized email must not be blank.", nameof(normalizedEmail));
        }

        ArgumentNullException.ThrowIfNull(profile);

        return new User(id, email, normalizedEmail, profile, now);
    }

    public void Activate(DateTimeOffset now)
    {
        if (IsActive)
        {
            return;
        }

        IsActive = true;
        UpdatedAt = now;
    }

    public void Deactivate(DateTimeOffset now)
    {
        if (!IsActive)
        {
            return;
        }

        IsActive = false;
        UpdatedAt = now;
    }

    public void UpdateProfile(
        string firstName,
        string lastName,
        string displayName,
        string? phoneNumber,
        string? avatarReference,
        DateTimeOffset now)
    {
        Profile.Update(firstName, lastName, displayName, phoneNumber, avatarReference, now);
        UpdatedAt = now;
    }
}
