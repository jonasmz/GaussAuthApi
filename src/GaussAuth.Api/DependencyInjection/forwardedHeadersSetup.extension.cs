using System.Net;
using GaussAuth.Infrastructure.Configuration;
using Microsoft.AspNetCore.HttpOverrides;

namespace GaussAuth.Api.DependencyInjection;

public static class ForwardedHeadersSetup
{
    public static IApplicationBuilder UseConfiguredForwardedHeaders(this IApplicationBuilder app)
    {
        var configuration = app.ApplicationServices.GetRequiredService<IConfiguration>();
        var proxies = configuration.GetSection("ForwardedHeaders:KnownProxies").Get<string[]>() ?? [];
        var networks = configuration.GetSection("ForwardedHeaders:KnownNetworks").Get<string[]>() ?? [];
        if (proxies.Length == 0 && networks.Length == 0)
        {
            if (IsTruthy(Environment.GetEnvironmentVariable("ASPNETCORE_FORWARDEDHEADERS_ENABLED")))
                throw new StartupConfigurationException("ForwardedHeaders:KnownProxies", "explicit trusted proxies or networks are required when forwarded headers are enabled");
            app.ApplicationServices.GetRequiredService<ILoggerFactory>().CreateLogger("GaussAuth.ForwardedHeaders")
                .LogWarning("Forwarded headers are ignored because no trusted proxy or network is configured; remote address and scheme may not represent the client behind a proxy.");
            return app;
        }

        var options = new ForwardedHeadersOptions { ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto, ForwardLimit = 1 };
        options.KnownProxies.Clear(); options.KnownIPNetworks.Clear();
        foreach (var value in proxies)
        {
            if (!IPAddress.TryParse(value, out var address)) throw new StartupConfigurationException("ForwardedHeaders:KnownProxies", "contains an invalid IP address");
            options.KnownProxies.Add(address);
        }
        foreach (var value in networks)
        {
            var parts = value.Split('/', 2);
            if (parts.Length != 2 || !IPAddress.TryParse(parts[0], out var address) || !int.TryParse(parts[1], out var prefix) || prefix < 0 || prefix > address.GetAddressBytes().Length * 8 || prefix == 0)
                throw new StartupConfigurationException("ForwardedHeaders:KnownNetworks", "contains an invalid or trust-anyone network");
            options.KnownIPNetworks.Add(new System.Net.IPNetwork(address, prefix));
        }
        return app.UseForwardedHeaders(options);
    }

    private static bool IsTruthy(string? value) => value is not null && value.Equals("true", StringComparison.OrdinalIgnoreCase) || value == "1";
}
