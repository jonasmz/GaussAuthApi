using GaussAuth.Application.Users.Ports;
using GaussAuth.Domain.Users;
using Microsoft.EntityFrameworkCore;

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
}
