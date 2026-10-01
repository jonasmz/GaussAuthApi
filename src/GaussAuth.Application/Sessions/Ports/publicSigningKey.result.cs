namespace GaussAuth.Application.Sessions.Ports;

public sealed record PublicSigningKey(string KeyId, string KeyType, string Curve, string Algorithm, string Use, string X, string Y);
