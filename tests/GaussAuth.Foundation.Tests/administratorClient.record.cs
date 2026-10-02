namespace GaussAuth.Foundation.Tests;

/// <summary>A signed-in administrative caller: an HTTP client carrying the bearer credential plus the identifiers behind it.</summary>
internal sealed record AdministratorClient(HttpClient Client, Guid UserId, Guid ApplicationId, string ApplicationCode, string Email, Guid SessionId, string AccessToken) : IDisposable
{
    public void Dispose() => Client.Dispose();
}
