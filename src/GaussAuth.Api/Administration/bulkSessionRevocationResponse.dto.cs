namespace GaussAuth.Api.Administration;

public sealed record BulkSessionRevocationResponse(int Revoked, bool HasMore);
