using GaussAuth.Application.Users.ActivateUser;
using GaussAuth.Application.Users.CreateUser;
using GaussAuth.Application.Users.DeactivateUser;
using GaussAuth.Application.Users.GetUser;
using GaussAuth.Application.Users.Profiles;

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
        return services;
    }
}
