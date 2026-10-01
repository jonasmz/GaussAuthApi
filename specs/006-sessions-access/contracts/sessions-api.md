# Sessions and Access API Contract

This contract extends the existing login route and adds the session routes. It introduces **no** OAuth 2.0 or OpenID Connect endpoints, no refresh tokens, and no roles/permissions in the credential. Error responses use safe Problem Details and never distinguish why a credential was rejected.

## Routes

| Method and route | Authentication | Purpose |
|---|---|---|
| `POST /auth/login` (extended) | none (rate limited, as in 005) | Authenticate, create a session, issue the first access credential |
| `POST /auth/session/renew` | `Authorization: Bearer <credential>` | Issue a new short-lived credential for the same session |
| `POST /auth/session/validate` | `Authorization: Bearer <credential>` | Authoritative check used by consuming APIs |
| `POST /auth/logout` | `Authorization: Bearer <credential>` | Revoke the credential's session |
| `GET /auth/signing-keys` | none (rate limited) | Public verification keys for local signature checks |

## `POST /auth/login` (extension of 005)

Request, validation, `400`, `401` (uniform), and `429` behavior are unchanged from `005-authentication-login`. After a successful authentication the service creates a session and issues a credential; if the user, application, or membership is no longer eligible at that instant, no session or credential is created and the same uniform `401` is returned.

Success `200` (additive fields; `userId` and `applicationId` are unchanged):

```json
{
  "userId": "guid",
  "applicationId": "guid",
  "sessionId": "guid",
  "tokenType": "Bearer",
  "accessToken": "<compact signed token>",
  "expiresAt": "2026-10-01T12:15:00Z",
  "sessionExpiresAt": "2026-10-01T20:00:00Z"
}
```

`Cache-Control: no-store` is set. The credential is returned only here and on renewal; it is never stored by the service.

## `POST /auth/session/renew`

No request body. Preconditions: a signature-valid, not-yet-expired credential whose session passes the authoritative check below.

Success `200`:

```json
{
  "sessionId": "guid",
  "tokenType": "Bearer",
  "accessToken": "<compact signed token>",
  "expiresAt": "2026-10-01T12:30:00Z",
  "sessionExpiresAt": "2026-10-01T20:00:00Z"
}
```

`expiresAt` is `min(now + access lifetime, sessionExpiresAt)`. Renewal never extends `sessionExpiresAt`. `Cache-Control: no-store`. Failure: `401` (uniform). An already expired credential cannot be renewed; the user logs in again.

## `POST /auth/session/validate`

Request body:

```json
{ "applicationCode": "acme" }
```

- `applicationCode`: required, maximum 64 characters; the stable code of the application the **caller** serves. The credential is rejected if it was issued for any other application.

Success `200`:

```json
{
  "userId": "guid",
  "applicationId": "guid",
  "sessionId": "guid",
  "expiresAt": "2026-10-01T12:15:00Z"
}
```

Failures: `400` invalid body (field problems only; never echoes the credential); `401` uniform rejection. Order: a missing or malformed `Authorization` header is `401`; an invalid body is `400`.

### Authoritative check (shared by renew and validate)

The service accepts the credential only when **all** hold: signature, issuer, and algorithm (ES256 only) are valid; the credential has not expired by server time; the session exists and matches the credential's `sid`, `sub`, and `aud`; (validate) the supplied `applicationCode` resolves to the session's application; the session is not revoked and not past `ExpiresAt`; the user is active; the application is active; and the user's membership in that application is active. Any failure yields the same `401`.

## `POST /auth/logout`

No request body. Revokes the session of a signature-valid credential.

| Case | Result |
|---|---|
| Credential valid, session active | `204`, session revoked durably |
| Credential signature-valid but token expired, session unknown, already revoked, or already expired | `204` (idempotent, reveals nothing) |
| Missing, malformed, or forged credential | `401` (uniform) |

After a `204` for an active session, that session's credentials fail the authoritative check immediately, including credentials not yet past `exp`.

## `GET /auth/signing-keys`

Success `200`, a standard JSON Web Key Set:

```json
{
  "keys": [
    { "kty": "EC", "crv": "P-256", "use": "sig", "alg": "ES256", "kid": "<thumbprint>", "x": "<base64url>", "y": "<base64url>" }
  ]
}
```

Contains public key material only. `Cache-Control: public, max-age=300`. `429` when the dedicated rate limit is exceeded.

## Failure shapes

All credential rejections (`validate`, `renew`, `logout` with a bad credential) are:

- Status `401`, standard Problem Details with title `"Access is not valid."`, header `WWW-Authenticate: Bearer`.
- Identical for: missing header, malformed or forged credential, expired credential, unknown session, revoked session, expired session, application mismatch, inactive user, inactive application, inactive membership. Internal causes appear only in structured logs and security events.

Unexpected infrastructure failures use the existing `SafeExceptionHandler` `500` shape (no stack traces, SQL, EF Core text, keys, or credentials).

## Credential contract for consuming APIs

Format: compact JWS, header `alg=ES256`, `typ=JWT`, `kid`. Claims: `iss` (configured issuer), `sub` (user id), `aud` (application id), `sid` (session id), `iat`, `exp`. No roles or permissions.

A consuming API MUST:

1. Verify the signature using a key from `GET /auth/signing-keys` (ES256 only; reject any other `alg`, including `none`), `iss`, and `exp`.
2. Verify `aud` equals its own application id.
3. For any operation that must honor revocation or eligibility loss, call `POST /auth/session/validate` with its application code. It MAY cache a positive result per `sid` for at most **60 seconds** (recommended **30**), never beyond `exp`, and MUST NOT cache failures. Revocation and deactivation therefore take effect within the consumer's cache window.
4. A consumer that performs only step 1-2 is bounded by the credential lifetime (default 15 minutes) and MUST NOT use that mode for revocation-sensitive operations.
5. Resolve roles and permissions separately from current application-scoped data; the consumer lookup contract is defined by `008-authorization-contract`.

## Configuration (external, never committed)

| Key | Default | Meaning |
|---|---|---|
| `Sessions:SessionLifetimeMinutes` | `480` | Absolute session lifetime (> 0) |
| `Sessions:AccessTokenLifetimeMinutes` | `15` | Credential lifetime (> 0, <= session lifetime) |
| `Sessions:Issuer` | `gaussauth` | `iss` claim value |
| `Sessions:Signing:PrivateKeyPem` | none | ES256 private key (PEM text). Secret; supply via environment/secret store |
| `Sessions:Signing:PrivateKeyPemFile` | none | Path to a PEM file (alternative to the line above) |
| `RateLimiting:SigningKeys:PermitLimit` | `60` | Requests per window per IP for `GET /auth/signing-keys` |
| `RateLimiting:SigningKeys:WindowSeconds` | `60` | Window length |

Startup fails (generic message, no values) when a lifetime is zero, negative, or inconsistent, or when no valid key is configured outside Development. In Development with no key, an ephemeral key is generated with a warning.
