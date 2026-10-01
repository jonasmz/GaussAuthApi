# Users API Contract: 002-users-profiles

All routes are grouped under `/users`. None of these routes perform caller
authentication or authorization in this feature (FR-023); they are reachable
only by trusted administrative/system callers until a future authentication/
authorization feature adds request-level access control. `POST /users` is
the one exception to "no additional control": because it creates a
credential with no caller authentication, it is rate-limited (FR-024, see
below). None of the five routes query or expose anything beyond what is
listed below — in particular, no password hash, security stamp, or other
Identity credential internal ever appears in a response or log.

## Shared response shape: `UserResponse`

```json
{
  "id": "guid",
  "email": "string",
  "normalizedEmail": "string",
  "isActive": true,
  "createdAt": "2026-10-01T00:00:00Z",
  "updatedAt": "2026-10-01T00:00:00Z",
  "profile": {
    "firstName": "string",
    "lastName": "string",
    "displayName": "string",
    "phoneNumber": "string|null",
    "avatarReference": "string|null",
    "createdAt": "2026-10-01T00:00:00Z",
    "updatedAt": "2026-10-01T00:00:00Z"
  }
}
```

## POST /users

Create a global user and its profile (FR-008/FR-009).

This operation creates an Identity credential and enforces no caller
authentication (FR-023), so it is rate-limited per caller (FR-024) — see
"Rate limiting" below.

**Request body**:

```json
{
  "email": "string (required, max 256, valid email format)",
  "password": "string (required, max 128; Identity's policy applies)",
  "firstName": "string (required, max 100)",
  "lastName": "string (required, max 100)",
  "displayName": "string (required, max 100)",
  "phoneNumber": "string|null (optional, max 32)",
  "avatarReference": "string|null (optional, max 2048)"
}
```

| Condition | Response |
|---|---|
| Valid, unique normalized email | `201 Created`, `Location: /users/{id}`, body: `UserResponse`. |
| Missing/invalid field (including a malformed email), or password fails Identity's policy | `400 Bad Request`, `ValidationProblemDetails` (field-level errors). |
| Normalized email already exists (including a concurrent race) | `409 Conflict`, generic Problem Details; does not reveal which field collided beyond "email". |
| Caller exceeds the configured rate limit | `429 Too Many Requests` with a `Retry-After` header; no user, profile, or credential is created (FR-024). |
| Unexpected failure | `500`, generic Problem Details via the existing safe exception handler; no partial user, profile, or credential row is left (one transaction, see `research.md`). |

**Rate limiting**: A fixed-window limiter (default: 5 requests per 60
seconds per caller IP, both configurable — see `research.md`'s "Rate
limiting for anonymous credential creation") applies only to this route,
since it is the sole operation in this feature that creates a credential.

## GET /users/{id}

Retrieve a user and its profile by stable identifier (FR-011).

| Condition | Response |
|---|---|
| User exists | `200 OK`, body: `UserResponse` (works for both active and inactive users). |
| No user with that identifier | `404 Not Found`, generic Problem Details; no persistence detail revealed. |

## PUT /users/{id}/profile

Replace the permitted profile fields (FR-012/FR-013). Full replacement of
the mutable field set; does not accept or alter `email`.

**Request body**:

```json
{
  "firstName": "string (required, max 100)",
  "lastName": "string (required, max 100)",
  "displayName": "string (required, max 100)",
  "phoneNumber": "string|null (optional, max 32)",
  "avatarReference": "string|null (optional, max 2048)"
}
```

| Condition | Response |
|---|---|
| User exists, body valid | `200 OK`, body: updated `UserResponse`; `profile.updatedAt` advances; `email`/`isActive`/credential state unchanged. |
| Request includes an `email` field or any credential field | `400 Bad Request` — the field is rejected/ignored as not part of this contract; login email is immutable through this operation (FR-013). |
| Missing/invalid field | `400 Bad Request`, `ValidationProblemDetails`; existing profile values remain unchanged. |
| No user with that identifier | `404 Not Found`. |
| Concurrent updates to the same user | Both may succeed; the last successfully persisted write prevails (last-write-wins, clarified 2026-10-01) — no `409` is produced for this reason. |

## POST /users/{id}/activate

Activate a user (FR-014). Idempotent.

| Condition | Response |
|---|---|
| User exists, was inactive | `200 OK`, body: `UserResponse` with `isActive: true`, `updatedAt` advanced. |
| User exists, already active | `200 OK`, body: `UserResponse` with `isActive: true`, `updatedAt` **unchanged** — no-op success, not an error (clarified 2026-10-01). |
| No user with that identifier | `404 Not Found`. |

## POST /users/{id}/deactivate

Deactivate a user (FR-014). Idempotent. Does not delete the user or profile.

| Condition | Response |
|---|---|
| User exists, was active | `200 OK`, body: `UserResponse` with `isActive: false`, `updatedAt` advanced. |
| User exists, already inactive | `200 OK`, body: `UserResponse` with `isActive: false`, `updatedAt` **unchanged** — no-op success, not an error (clarified 2026-10-01). |
| No user with that identifier | `404 Not Found`. |

## General error shape

As established by `001-foundation`: unexpected errors use the built-in
ASP.NET Core Problem Details contract with HTTP 500 and reveal no exception
message, stack trace, SQL, connection string, physical path, secret, or
credential data. Expected failures (validation, duplicate email, not found)
use the status codes above with a generic, non-sensitive Problem Details
body — never a password hash, security stamp, or other Identity internal.
