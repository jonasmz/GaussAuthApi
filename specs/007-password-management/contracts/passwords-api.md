# Password Management API Contract

| Route | Authentication | Request | Success | Safe failure |
|---|---|---|---|---|
| `POST /auth/password/change` | Bearer credential | `currentPassword`, `newPassword` (required, max 128) | `204`; revoke all sessions | existing uniform `401`; safe `400` for current-password, inactive-user, or policy failure |
| `POST /auth/password/recovery` | Public, rate limited | `email` (valid, max 256) | always `202` with generic message | syntactic validation `400`; no enumeration |
| `POST /auth/password/reset` | Public, rate limited | `email` max 256, `recoveryCredential` max 4096, `newPassword` max 128 | `204`; clear lockout and revoke all sessions | one safe `400` titled `Password reset failed.` |

Passwords and recovery credentials are never logged, returned, persisted by application code, or placed in events.
