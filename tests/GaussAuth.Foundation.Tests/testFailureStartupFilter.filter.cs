using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace GaussAuth.Foundation.Tests;

public sealed class TestFailureStartupFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        return application =>
        {
            application.UseExceptionHandler();
            application.Run(_ => throw new InvalidOperationException(
                "SELECT sensitive; /private/server/path; not_a_secret"));
            next(application);
        };
    }
}
