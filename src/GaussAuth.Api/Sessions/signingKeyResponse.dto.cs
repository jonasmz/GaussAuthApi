using System.Text.Json.Serialization;

namespace GaussAuth.Api.Sessions;

public sealed record SigningKeyResponse(
    [property: JsonPropertyName("kty")] string KeyType,
    [property: JsonPropertyName("crv")] string Curve,
    [property: JsonPropertyName("use")] string Use,
    [property: JsonPropertyName("alg")] string Algorithm,
    [property: JsonPropertyName("kid")] string KeyId,
    [property: JsonPropertyName("x")] string X,
    [property: JsonPropertyName("y")] string Y);
