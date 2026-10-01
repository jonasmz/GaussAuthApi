# Authentication Login API Contract

This route is the single public, unauthenticated capability of this feature. It adds no session, token, or caller-authorization model. Error responses use safe Problem Details and never distinguish credential/account-state failure causes externally.

## Login Route

| Method and route | Request | Success | Expected failures |
|---|---|---|---|
| `POST /auth/login` | `{ "applicationCode": "acme", "email": "user@example.test", "password": "..." }` | `200` login result | `400` invalid input (missing/malformed field, length exceeded); `401` uniform credential/account-state rejection (unknown email, wrong password, inactive user, invalid/inactive application, missing/inactive membership, or account lockout — not distinguished); `429` rate limit exceeded (rejected before credential validation). |

## Request Shape

```json
{
  "applicationCode": "acme",
  "email": "user@example.test",
  "password": "P@ssw0rd!"
}
```

- `applicationCode`: required, maximum 64 characters, the stable `003-applications-memberships` application code.
- `email`: required, must be a syntactically valid email address, maximum 256 characters. Normalized server-side before resolution; callers do not need to pre-normalize it.
- `password`: required, maximum 128 characters. Never echoed back in any response, including `400` validation failures.

## Success Response Shape

```json
{
  "userId": "guid",
  "applicationId": "guid"
}
```

Contains only the two stable identifiers needed by `006-sessions-access` to establish a session. No roles, permissions, profile data, or credential details are included.

## Failure Response Shapes

`400` (invalid input) and `429` (rate limit) use the project's standard Problem Details shape already used by other endpoints, with no field revealing the submitted password.

`401` (uniform credential/account-state rejection) also uses the standard Problem Details shape and is identical in body/status for every one of these causes:

- the email does not correspond to any registered user;
- the password does not match;
- the user is inactive;
- the application code does not resolve to any application, or the application is inactive;
- the user has no membership, or an inactive membership, in the target application;
- the account is currently locked out by ASP.NET Core Identity.

Per the 2026-10-01 clarification, rate-limit rejections (`429`) are a separate request-layer protection and are intentionally not required to share this exact shape with the `401` outcome, since they occur before the authentication use case runs.

## Rate Limiting

The route is protected by a dedicated `login` rate-limiting policy (fixed window, partitioned by remote IP address, configurable `PermitLimit`/`WindowSeconds`), mirroring the existing `user-creation` policy. A `429` response includes a `Retry-After` header, consistent with the existing policy's rejection handling.

## Account Lockout

Repeated invalid-password attempts against one account are tracked by ASP.NET Core Identity's built-in lockout mechanism (configurable `MaxFailedAccessAttempts`/`DefaultLockoutMinutes`). A currently locked-out account always receives the same `401` uniform rejection described above, even when the submitted password is correct.
