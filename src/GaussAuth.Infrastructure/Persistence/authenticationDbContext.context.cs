using GaussAuth.Domain.Users;
using DomainApplication = GaussAuth.Domain.Applications.Application;
using GaussAuth.Domain.Memberships;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace GaussAuth.Infrastructure.Persistence;

public sealed class AuthenticationDbContext(DbContextOptions<AuthenticationDbContext> options)
    : IdentityUserContext<IdentityUser<Guid>, Guid>(options)
{
    public DbSet<User> DomainUsers => Set<User>();

    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
    public DbSet<DomainApplication> Applications => Set<DomainApplication>();
    public DbSet<ApplicationMembership> ApplicationMemberships => Set<ApplicationMembership>();

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

        builder.Entity<DomainApplication>(application =>
        {
            application.ToTable("Applications");
            application.HasKey(x => x.Id);
            application.Property(x => x.Id).ValueGeneratedNever();
            application.Property(x => x.Code).HasMaxLength(64).IsRequired();
            application.Property(x => x.Name).HasMaxLength(200).IsRequired();
            application.HasIndex(x => x.Code).IsUnique().HasDatabaseName("IX_Applications_Code");
        });

        builder.Entity<ApplicationMembership>(membership =>
        {
            membership.ToTable("ApplicationMemberships");
            membership.HasKey(x => x.Id);
            membership.Property(x => x.Id).ValueGeneratedNever();
            membership.HasIndex(x => new { x.UserId, x.ApplicationId }).IsUnique().HasDatabaseName("IX_ApplicationMemberships_UserId_ApplicationId");
            membership.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            membership.HasOne<DomainApplication>().WithMany().HasForeignKey(x => x.ApplicationId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
