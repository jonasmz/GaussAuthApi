using System.Net.Http.Headers;

namespace GaussAuth.Foundation.Tests;

/// <summary>
/// Adds the administrator's bearer credential to requests for <c>/admin</c> routes only, so a test can keep using one client
/// for both administrative setup and calls that act as another signed-in user (their own <c>Authorization</c> header).
/// </summary>
internal sealed class AdministratorBearerHandler(string accessToken) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.RequestUri is { } uri && uri.AbsolutePath.StartsWith("/admin/", StringComparison.Ordinal))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return base.SendAsync(request, cancellationToken);
    }
}
