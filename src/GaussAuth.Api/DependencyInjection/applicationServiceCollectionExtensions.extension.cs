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

namespace GaussAuth.Api.DependencyInjection;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<CreateUserHandler>();
        services.AddScoped<GetUserHandler>();
        services.AddScoped<UpdateProfileHandler>();
        services.AddScoped<ActivateUserHandler>();
        services.AddScoped<DeactivateUserHandler>();
        services.AddScoped<ApplicationService>();
        services.AddScoped<MembershipService>();
        services.AddScoped<RoleService>();
        services.AddScoped<PermissionService>();
        services.AddScoped<RolePermissionService>();
        services.AddScoped<UserRoleService>();
        return services;
    }
}
