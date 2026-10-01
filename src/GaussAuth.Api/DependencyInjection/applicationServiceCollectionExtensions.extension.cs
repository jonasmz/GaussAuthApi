using GaussAuth.Application.Users.CreateUser;

namespace GaussAuth.Api.DependencyInjection;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<CreateUserHandler>();
        return services;
    }
}
