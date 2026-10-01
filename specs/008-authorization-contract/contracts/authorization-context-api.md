# Authorization Context API Contract

## `POST /auth/authorization-context`

### Required headers

| Header | Meaning |
|---|---|
| `Authorization` | `Bearer` user access credential; never log it. |
| `X-GaussAuth-Application-Code` | Consumer's configured application code; required, nonblank, maximum 64 characters. |
| `X-GaussAuth-Consumer-Secret` | Independent externally configured service credential for that application; required, nonblank, maximum 512 characters; never log or return it. |

### Success: `200 OK`

```json
{
  "userId": "guid",
  "applicationId": "guid",
  "sessionId": "guid",
  "issuedAt": "timestamp",
  "credentialExpiresAt": "timestamp",
  "sessionExpiresAt": "timestamp",
  "roles": [{ "id": "guid", "name": "operator" }],
  "permissions": ["orders.create", "orders.read"]
}
```

Roles are current active assignments and permissions are current unique active codes for exactly the requested application. No PII, Auth persistence model, Identity type, security stamp, key material, or secret is included.

### Failure: `401 Unauthorized`

Missing/invalid consumer credential, wrong consumer application, invalid user credential, expired/revoked session, inactive state, or application mismatch return the established generic unauthorized result with `WWW-Authenticate: Bearer`. The response does not identify the failing check.

Requests exceeding the configurable authorization-context limit return `429 Too Many Requests`. The default is 600 requests per remote IP in a 60-second fixed window.

### Consumer responsibilities

1. Configure the expected application code and independent service credential outside source control. Do not put either value in source, images, responses, or logs.
2. Receive the user bearer credential from the inbound request as untrusted input; do not parse, verify, or turn it into permission claims locally.
3. Forward that bearer credential only to this endpoint with `X-GaussAuth-Application-Code` and `X-GaussAuth-Consumer-Secret` over the protected service-to-service path.
4. Treat only a `200 OK` response as authoritative. Verify returned `applicationId` is the configured application's stable ID before using the context.
5. Enforce each consumer-owned business operation by requiring its named generic code in `permissions`; Auth does not decide the business operation.
6. Deny the operation on `401`, `429`, transport failure, malformed response, missing required permission, or an application-ID mismatch. Never fall back to a stale or locally decoded credential.
7. Respect `credentialExpiresAt` and `sessionExpiresAt` as returned metadata, but obtain a new context whenever a current authorization decision is required because sessions, roles, memberships, and permissions can change or be revoked immediately.
8. Never query Auth PostgreSQL, create a foreign key to it, duplicate password/token validation, or reference Auth persistence/Identity/session models. Store `userId` only as a logical value in consumer-owned records.
9. Do not cache a context in a way that weakens the current authorization or revocation policy. A new operation requiring current authorization must obtain a new context.
10. Rotate the service credential by configuring a new current value, temporarily accepting the retiring value for the same application, deploying the new value to consumers, then removing the retiring value. The application identity, user credential, headers, and response shape do not change.

### Permission-check example

The consuming API owns the operation and simply checks the public `permissions` array:

```csharp
var allowed = context.Permissions.Contains("orders.create", StringComparer.Ordinal);
if (!allowed) return Results.Forbid();
```

This code uses the published DTO only; it has no connection to Auth persistence.

### Rejection and freshness

The endpoint deliberately makes invalid consumer credentials, wrong application identity, invalid or expired user credentials, revoked sessions, and inactive authorization state indistinguishable as `401`. A consumer must deny safely and must not reveal which prerequisite failed. Roles and permissions are evaluated during every successful context request, so a later request reflects assignment removal and role or permission deactivation without a consumer-side cache.

### Rotation

Auth accepts a current and temporary retiring secret only for the same application. Deploy the new value to the consumer, confirm it succeeds, and remove the retiring value after transition; the request shape and user credentials remain unchanged. Never send either value to a browser or persist it with user data.
