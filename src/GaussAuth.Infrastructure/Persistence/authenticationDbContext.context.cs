using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace GaussAuth.Infrastructure.Persistence;

public sealed class AuthenticationDbContext(DbContextOptions<AuthenticationDbContext> options)
    : IdentityUserContext<IdentityUser<Guid>, Guid>(options)
{
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Entity<IdentityUser<Guid>>()
            .HasIndex(user => user.NormalizedEmail)
            .IsUnique();
    }
}
