namespace GaussAuth.Api.Sessions;

public static class BearerCredentialReader
{
    public static string? ReadBearerCredential(this HttpRequest request)
    {
        var header = request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(header) || header.Length > 4096) return null;
        const string scheme = "Bearer ";
        if (!header.StartsWith(scheme, StringComparison.OrdinalIgnoreCase)) return null;
        var credential = header[scheme.Length..].Trim();
        return credential.Length is > 0 and <= 4096 ? credential : null;
    }
}
