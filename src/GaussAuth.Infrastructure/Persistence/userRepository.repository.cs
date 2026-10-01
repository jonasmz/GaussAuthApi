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
