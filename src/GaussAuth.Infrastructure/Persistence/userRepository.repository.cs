using GaussAuth.Application.Users.Ports;
using GaussAuth.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace GaussAuth.Infrastructure.Persistence;

public sealed class UserRepository(AuthenticationDbContext context) : IUserRepository
{
    public Task<bool> ExistsByNormalizedEmailAsync(string normalizedEmail, CancellationToken cancellationToken) =>
        context.DomainUsers.AnyAsync(user => user.NormalizedEmail == normalizedEmail, cancellationToken);

    public async Task AddAsync(User user, CancellationToken cancellationToken) =>
        await context.DomainUsers.AddAsync(user, cancellationToken);

    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        context.DomainUsers
            .Include(user => user.Profile)
            .SingleOrDefaultAsync(user => user.Id == id, cancellationToken);

    public async Task<User?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken)
    {
        if (context.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("A transaction is required to lock a user profile.");
        }

        // Take the lock in its own statement, then read in a fresh statement: under READ COMMITTED a
        // single locking query would still return the profile row as of its pre-wait snapshot.
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT \"Id\" FROM \"Users\" WHERE \"Id\" = {id} FOR UPDATE", cancellationToken);

        // A copy tracked before the lock (for example by authentication) may be stale; discard it so
        // the read below materializes the committed state observed after acquiring the lock.
        foreach (var entry in context.ChangeTracker.Entries()
                     .Where(entry => entry.Entity is User { } tracked && tracked.Id == id ||
                                     entry.Entity is UserProfile { } profile && profile.UserId == id)
                     .ToList())
        {
            entry.State = EntityState.Detached;
        }

        return await context.DomainUsers
            .Include(user => user.Profile)
            .SingleOrDefaultAsync(user => user.Id == id, cancellationToken);
    }

    public Task<User?> GetByNormalizedEmailAsync(string normalizedEmail, CancellationToken cancellationToken) =>
        context.DomainUsers
            .Include(user => user.Profile)
            .SingleOrDefaultAsync(user => user.NormalizedEmail == normalizedEmail, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        context.SaveChangesAsync(cancellationToken);

    public async Task<bool> TrySaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException exception) when (IsNormalizedEmailUniqueViolation(exception))
        {
            return false;
        }
    }

    private static bool IsNormalizedEmailUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } postgresException &&
        postgresException.ConstraintName is "IX_Users_NormalizedEmail";

    public async Task<IUserRepositoryTransaction> BeginTransactionAsync(CancellationToken cancellationToken)
    {
        var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        return new EfUserRepositoryTransaction(transaction);
    }
}
