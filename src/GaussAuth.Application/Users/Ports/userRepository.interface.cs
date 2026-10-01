using GaussAuth.Domain.Users;

namespace GaussAuth.Application.Users.Ports;

public interface IUserRepository
{
    Task<bool> ExistsByNormalizedEmailAsync(string normalizedEmail, CancellationToken cancellationToken);

    Task AddAsync(User user, CancellationToken cancellationToken);

    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<User?> GetByNormalizedEmailAsync(string normalizedEmail, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Persists pending changes, returning <see langword="false"/> instead of throwing when
    /// persistence rejects the save because of the normalized-email uniqueness constraint
    /// (the concurrent-creation race window). Any other failure still propagates.
    /// </summary>
    Task<bool> TrySaveChangesAsync(CancellationToken cancellationToken);

    Task<IUserRepositoryTransaction> BeginTransactionAsync(CancellationToken cancellationToken);
}
