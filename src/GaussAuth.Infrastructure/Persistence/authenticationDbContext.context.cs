using GaussAuth.Domain.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace GaussAuth.Infrastructure.Persistence;

public sealed class AuthenticationDbContext(DbContextOptions<AuthenticationDbContext> options)
    : IdentityUserContext<IdentityUser<Guid>, Guid>(options)
{
    public DbSet<User> DomainUsers => Set<User>();

    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Entity<IdentityUser<Guid>>()
            .HasIndex(user => user.NormalizedEmail)
            .IsUnique();

        builder.Entity<User>(user =>
        {
            user.ToTable("Users");
            user.HasKey(u => u.Id);
            user.Property(u => u.Id).ValueGeneratedNever();
            user.HasIndex(u => u.NormalizedEmail).IsUnique();
            user.HasOne<IdentityUser<Guid>>()
                .WithOne()
                .HasForeignKey<User>(u => u.Id)
                .OnDelete(DeleteBehavior.Cascade);
            user.HasOne(u => u.Profile)
                .WithOne()
                .HasForeignKey<UserProfile>(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<UserProfile>(profile =>
        {
            profile.ToTable("UserProfiles");
            profile.HasKey(p => p.UserId);
            profile.Property(p => p.UserId).ValueGeneratedNever();
        });
    }
}
