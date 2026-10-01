using GaussAuth.Application.Sessions.Ports;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace GaussAuth.Infrastructure.Sessions;

public sealed class SignedAccessCredentialValidator(AccessCredentialSigningKey signingKey, string issuer) : IAccessCredentialValidator
{
    private static readonly JsonWebTokenHandler Handler = new();

    public async Task<AccessCredentialClaims?> ValidateAsync(string credential, CancellationToken cancellationToken)
    {
        try
        {
            var result = await Handler.ValidateTokenAsync(credential, new TokenValidationParameters
            {
                RequireSignedTokens = true,
                ValidAlgorithms = [SecurityAlgorithms.EcdsaSha256],
                IssuerSigningKey = signingKey.SecurityKey,
                ValidIssuer = issuer,
                ValidateIssuer = true,
                ValidateAudience = false,
                ValidateLifetime = false,
                RequireExpirationTime = true
            });
            if (!result.IsValid || result.SecurityToken is not JsonWebToken token) return null;
            if (!Guid.TryParse(token.Subject, out var userId) || !Guid.TryParse(token.GetPayloadValue<string>("aud"), out var applicationId) ||
                !Guid.TryParse(token.GetPayloadValue<string>("sid"), out var sessionId)) return null;
            var iat = token.GetPayloadValue<long>("iat");
            var exp = token.GetPayloadValue<long>("exp");
            return new AccessCredentialClaims(sessionId, userId, applicationId, DateTimeOffset.FromUnixTimeSeconds(iat), DateTimeOffset.FromUnixTimeSeconds(exp));
        }
        catch (Exception)
        {
            return null;
        }
    }
}
