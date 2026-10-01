using GaussAuth.Application.Administration.Authorization;
using GaussAuth.Application.Administration.Bootstrap;
using GaussAuth.Application.Administration.Ports;
using GaussAuth.Application.Administration.Users.ListUsers;
using GaussAuth.Application.Users.ActivateUser;
using GaussAuth.Application.Users.CreateUser;
using GaussAuth.Application.Users.DeactivateUser;
using GaussAuth.Application.Users.GetUser;
using GaussAuth.Application.Users.Profiles;
using GaussAuth.Application.Applications;
using GaussAuth.Application.Memberships;
using GaussAuth.Application.Roles;
using GaussAuth.Application.Permissions;
using GaussAuth.Application.Authorization;
using GaussAuth.Application.AuthorizationContext;
using GaussAuth.Application.Login;
using GaussAuth.Application.Sessions;
using GaussAuth.Application.Sessions.Ports;
using GaussAuth.Application.Passwords;
using GaussAuth.Application.Security;
using GaussAuth.Application.Profiles.Avatars;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace GaussAuth.Api.DependencyInjection;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<CreateUserHandler>();
        services.AddScoped<GetUserHandler>();
        services.AddScoped<ListUsersHandler>();
        services.AddScoped<UpdateProfileHandler>();
        services.AddScoped<ActivateUserHandler>();
        services.AddScoped<DeactivateUserHandler>();
        services.AddScoped<ApplicationService>();
        services.AddScoped<MembershipService>();
        services.AddScoped<RoleService>();
        services.AddScoped<PermissionService>();
        services.AddScoped<RolePermissionService>();
        services.AddScoped<UserRoleService>();
        services.AddScoped<EffectivePermissionService>();
        services.AddScoped<AuthorizationContextService>();
        services.AddScoped<LoginService>();
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<SessionService>();
        services.AddScoped<ISessionRevoker>(provider => provider.GetRequiredService<SessionService>());
        services.AddScoped<PasswordManagementService>();
        services.AddScoped<SecurityEventQueryService>();
        services.AddScoped<ProfileAvatarService>();
        services.AddScoped<ProfileAvatarReadService>();
        services.AddScoped<AdministrativePermissionBootstrap>();
        services.AddScoped<AdministrativeAuthorizer>();
        services.AddScoped<AdministrativeActorContext>();
        services.AddScoped<IAdministrativeActorContext>(provider => provider.GetRequiredService<AdministrativeActorContext>());
        return services;
    }
}
