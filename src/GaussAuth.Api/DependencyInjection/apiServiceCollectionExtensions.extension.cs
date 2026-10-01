namespace GaussAuth.Api.DependencyInjection;

public static class ApiServiceCollectionExtensions
{
    public static IServiceCollection AddApiServices(this IServiceCollection services)
    {
        services.AddProblemDetails();
        services.AddExceptionHandler<SafeExceptionHandler>();
        return services;
    }
}
