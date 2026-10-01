using GaussAuth.Application.Sessions.Ports;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace GaussAuth.Infrastructure.Sessions;

public sealed class SignedAccessCredentialIssuer(AccessCredentialSigningKey signingKey, string issuer) : IAccessCredentialIssuer
{
    private static readonly JsonWebTokenHandler Handler = new() { SetDefaultTimesOnTokenCreation = false };

    public string Issue(AccessCredentialClaims claims) => Handler.CreateToken(new SecurityTokenDescriptor
    {
        Issuer = issuer,
        Claims = new Dictionary<string, object>
        {
            ["sub"] = claims.UserId.ToString(),
            ["aud"] = claims.ApplicationId.ToString(),
            ["sid"] = claims.SessionId.ToString(),
            ["iat"] = claims.IssuedAt.ToUnixTimeSeconds(),
            ["exp"] = claims.ExpiresAt.ToUnixTimeSeconds()
        },
        SigningCredentials = new SigningCredentials(signingKey.SecurityKey, SecurityAlgorithms.EcdsaSha256)
    });
}
