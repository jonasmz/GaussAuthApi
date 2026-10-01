using System.Text.Json.Serialization;

namespace GaussAuth.Api.Sessions;

public sealed record SigningKeySetResponse([property: JsonPropertyName("keys")] IReadOnlyList<SigningKeyResponse> Keys);
