using GaussAuth.Domain.Applications;
using GaussAuth.Domain.Users;
using DomainApplication = GaussAuth.Domain.Applications.Application;
using GaussAuth.Domain.Memberships;
using GaussAuth.Domain.Roles;
using DomainPermission = GaussAuth.Domain.Permissions.Permission;
using GaussAuth.Domain.Authorization;
using GaussAuth.Domain.Sessions;
using GaussAuth.Domain.Security;
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
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<DomainPermission> Permissions => Set<DomainPermission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<ConsumerCredential> ConsumerCredentials => Set<ConsumerCredential>();
    public DbSet<Session> Sessions => Set<Session>();
    public DbSet<SecurityEvent> SecurityEvents => Set<SecurityEvent>();

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
            membership.HasAlternateKey(x => new { x.UserId, x.ApplicationId }).HasName("AK_ApplicationMemberships_UserId_ApplicationId");
            membership.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            membership.HasOne<DomainApplication>().WithMany().HasForeignKey(x => x.ApplicationId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Role>(role =>
        {
            role.ToTable("Roles"); role.HasKey(x => x.Id); role.Property(x => x.Id).ValueGeneratedNever();
            role.Property(x => x.Name).HasMaxLength(200).IsRequired(); role.Property(x => x.NormalizedName).HasMaxLength(200).IsRequired(); role.Property(x => x.Description).HasMaxLength(500);
            role.HasIndex(x => new { x.ApplicationId, x.NormalizedName }).IsUnique().HasDatabaseName("IX_Roles_ApplicationId_NormalizedName");
            role.HasAlternateKey(x => new { x.Id, x.ApplicationId }).HasName("AK_Roles_Id_ApplicationId");
            role.HasOne<DomainApplication>().WithMany().HasForeignKey(x => x.ApplicationId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<DomainPermission>(permission =>
        {
            permission.ToTable("Permissions"); permission.HasKey(x => x.Id); permission.Property(x => x.Id).ValueGeneratedNever();
            permission.Property(x => x.Code).HasMaxLength(128).IsRequired(); permission.Property(x => x.Description).HasMaxLength(500);
            permission.HasIndex(x => new { x.ApplicationId, x.Code }).IsUnique().HasDatabaseName("IX_Permissions_ApplicationId_Code");
            permission.HasAlternateKey(x => new { x.Id, x.ApplicationId }).HasName("AK_Permissions_Id_ApplicationId");
            permission.HasOne<DomainApplication>().WithMany().HasForeignKey(x => x.ApplicationId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ConsumerCredential>(credential =>
        {
            credential.ToTable("ApplicationConsumerCredentials"); credential.HasKey(x => x.ApplicationId);
            credential.Property(x => x.ApplicationId).ValueGeneratedNever();
            credential.Property(x => x.CurrentHash).HasMaxLength(ConsumerCredential.MaximumHashLength).IsRequired();
            credential.Property(x => x.RetiringHash).HasMaxLength(ConsumerCredential.MaximumHashLength);
            credential.Property<uint>("xmin").HasColumnName("xmin").HasColumnType("xid").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
            credential.HasOne<DomainApplication>().WithOne().HasForeignKey<ConsumerCredential>(x => x.ApplicationId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<RolePermission>(assignment =>
        {
            assignment.ToTable("RolePermissions"); assignment.HasKey(x => x.Id); assignment.Property(x => x.Id).ValueGeneratedNever();
            assignment.HasIndex(x => new { x.RoleId, x.PermissionId }).IsUnique().HasDatabaseName("IX_RolePermissions_RoleId_PermissionId");
            assignment.HasOne<Role>().WithMany().HasForeignKey(x => new { x.RoleId, x.ApplicationId }).HasPrincipalKey(x => new { x.Id, x.ApplicationId }).OnDelete(DeleteBehavior.Restrict);
            assignment.HasOne<DomainPermission>().WithMany().HasForeignKey(x => new { x.PermissionId, x.ApplicationId }).HasPrincipalKey(x => new { x.Id, x.ApplicationId }).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<UserRole>(assignment =>
        {
            assignment.ToTable("UserRoles"); assignment.HasKey(x => x.Id); assignment.Property(x => x.Id).ValueGeneratedNever();
            assignment.HasIndex(x => new { x.UserId, x.RoleId, x.ApplicationId }).IsUnique().HasDatabaseName("IX_UserRoles_UserId_RoleId_ApplicationId");
            assignment.HasOne<ApplicationMembership>().WithMany().HasForeignKey(x => new { x.UserId, x.ApplicationId }).HasPrincipalKey(x => new { x.UserId, x.ApplicationId }).OnDelete(DeleteBehavior.Restrict);
            assignment.HasOne<Role>().WithMany().HasForeignKey(x => new { x.RoleId, x.ApplicationId }).HasPrincipalKey(x => new { x.Id, x.ApplicationId }).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Session>(session =>
        {
            session.ToTable("Sessions"); session.HasKey(x => x.Id).HasName("PK_Sessions"); session.Property(x => x.Id).ValueGeneratedNever();
            session.Property(x => x.UserId).IsRequired(); session.Property(x => x.ApplicationId).IsRequired();
            session.Property(x => x.CreatedAt).IsRequired(); session.Property(x => x.ExpiresAt).IsRequired();
            session.HasIndex(x => new { x.UserId, x.ApplicationId }).HasDatabaseName("IX_Sessions_UserId_ApplicationId");
            session.HasOne<ApplicationMembership>().WithMany().HasForeignKey(x => new { x.UserId, x.ApplicationId }).HasPrincipalKey(x => new { x.UserId, x.ApplicationId }).HasConstraintName("FK_Sessions_ApplicationMemberships_UserId_ApplicationId").OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<SecurityEvent>(securityEvent =>
        {
            securityEvent.ToTable("SecurityEvents");
            securityEvent.HasKey(x => x.Id);
            securityEvent.Property(x => x.Id).ValueGeneratedNever();
            securityEvent.Property(x => x.EventType).HasMaxLength(128).IsRequired();
            securityEvent.Property(x => x.Outcome).HasMaxLength(16).IsRequired();
            securityEvent.Property(x => x.OccurredAtUtc).IsRequired();
            securityEvent.Property(x => x.CorrelationId).HasMaxLength(128);
            securityEvent.Property(x => x.SubjectType).HasMaxLength(64);
            securityEvent.Property(x => x.Reason).HasMaxLength(128);
            securityEvent.Property(x => x.Metadata).HasMaxLength(2048);
            securityEvent.HasIndex(x => new { x.OccurredAtUtc, x.Id }).HasDatabaseName("IX_SecurityEvents_OccurredAtUtc_Id");
            securityEvent.HasIndex(x => new { x.ApplicationId, x.OccurredAtUtc, x.Id }).HasDatabaseName("IX_SecurityEvents_ApplicationId_OccurredAtUtc_Id");
            securityEvent.HasIndex(x => new { x.UserId, x.OccurredAtUtc, x.Id }).HasDatabaseName("IX_SecurityEvents_UserId_OccurredAtUtc_Id");
            securityEvent.HasIndex(x => new { x.SessionId, x.OccurredAtUtc, x.Id }).HasDatabaseName("IX_SecurityEvents_SessionId_OccurredAtUtc_Id");
            securityEvent.HasIndex(x => new { x.EventType, x.OccurredAtUtc, x.Id }).HasDatabaseName("IX_SecurityEvents_EventType_OccurredAtUtc_Id");
            securityEvent.HasIndex(x => new { x.ActorUserId, x.OccurredAtUtc, x.Id }).HasDatabaseName("IX_SecurityEvents_ActorUserId_OccurredAtUtc_Id");
        });
    }
}
