# Quickstart Validation: Sessions and Access

Use the existing Docker workflow only (host daemon via the mounted socket; all .NET commands inside the `sdk` container). This guide assumes a private `.env` derived from `.env.example` and features 001-005 applied. Routes and shapes are defined in [contracts/sessions-api.md](contracts/sessions-api.md); entities in [data-model.md](data-model.md).

Shorthand used below: `DC="docker compose --env-file .env -f compose.dev.yml"` and `CURL="$DC exec -T sdk curl -sS -i"`.

## 1. Build, migrate, run

```sh
$DC up -d --wait postgres
$DC run --rm sdk dotnet build GaussAuth.slnx
$DC run --rm sdk dotnet ef database update --project src/GaussAuth.Infrastructure --startup-project src/GaussAuth.Api
$DC up -d sdk
```

Expected: build succeeds; the `AddSessions` migration applies and creates the `Sessions` table. Optional: set `SESSIONS_SIGNING_KEY_PEM` in `.env` to a private ES256 key; if it is empty the Development environment uses an ephemeral key (warning logged; credentials stop verifying after an API restart, persisted sessions are unaffected).

## 2. Prerequisites

Create a user, an application (`<APP_CODE>`, `<APP_ID>`), and an active membership exactly as in the 005 quickstart. Create a second application (`<APP2_CODE>`) with a membership for the same user.

## 3. Login creates a session and a credential

```sh
$CURL -X POST http://127.0.0.1:8080/auth/login -H 'Content-Type: application/json' \
  -d '{"applicationCode":"<APP_CODE>","email":"quickstart-login@example.test","password":"Quickstart!2026"}'
```

Expected: `200`, body has `userId`, `applicationId`, `sessionId`, `tokenType":"Bearer"`, `accessToken`, `expiresAt` (about 15 minutes ahead) and `sessionExpiresAt` (about 8 hours ahead); `Cache-Control: no-store`. Keep `<TOKEN>` and `<SESSION_ID>`. Run the login again: a different `sessionId` is returned and both sessions work independently.

## 4. Authoritative validation and application isolation

```sh
$CURL -X POST http://127.0.0.1:8080/auth/session/validate -H 'Authorization: Bearer <TOKEN>' \
  -H 'Content-Type: application/json' -d '{"applicationCode":"<APP_CODE>"}'

$CURL -X POST http://127.0.0.1:8080/auth/session/validate -H 'Authorization: Bearer <TOKEN>' \
  -H 'Content-Type: application/json' -d '{"applicationCode":"<APP2_CODE>"}'
```

Expected: first `200` with the same user, application, and session identifiers; second `401` (credential for another application), identical in shape to any other rejection. A tampered token (change one character of `<TOKEN>`) also returns `401`.

## 5. Public verification keys

```sh
$CURL http://127.0.0.1:8080/auth/signing-keys
```

Expected: `200` JWKS with one `EC`/`P-256`/`ES256` key whose `kid` equals the token header `kid`; no private material.

## 6. Renewal

```sh
$CURL -X POST http://127.0.0.1:8080/auth/session/renew -H 'Authorization: Bearer <TOKEN>'
```

Expected: `200` with the same `sessionId`, a new `accessToken`, and an unchanged `sessionExpiresAt`.

## 7. Eligibility changes take effect at the next check

```sh
$CURL -X POST http://127.0.0.1:8080/users/<USER_ID>/deactivate
# validate again -> 401
$CURL -X POST http://127.0.0.1:8080/users/<USER_ID>/activate
# validate again -> 200 (session was not revoked)
```

Repeat with `POST /applications/<APP_ID>/deactivate` and `/activate`, and with `POST /applications/<APP_ID>/memberships/<USER_ID>/deactivate` and `/activate`. Expected: `401` while inactive, `200` again after reactivation, with no change to the session record.

## 8. Logout is durable

```sh
$CURL -X POST http://127.0.0.1:8080/auth/logout -H 'Authorization: Bearer <TOKEN>'
# validate and renew with <TOKEN> -> 401
$CURL -X POST http://127.0.0.1:8080/auth/logout -H 'Authorization: Bearer <TOKEN>'   # idempotent -> 204
```

Expected: first logout `204`; the same token now fails validation and renewal even though it has not reached `exp`; a second login's session is unaffected; a repeated logout is `204`; restarting the `sdk` service (with a configured key) leaves the session revoked. Inspect persistence without exposing secrets:

```sh
$DC exec -T postgres sh -c 'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -c "select \"Id\",\"UserId\",\"ApplicationId\",\"ExpiresAt\",\"RevokedAt\" from \"Sessions\" order by \"CreatedAt\""'
```

## 9. No credential in logs

```sh
$DC logs sdk | grep -c "<TOKEN>"
```

Expected: `0`. Logs show only user, application, and session identifiers with event types.

## 10. Automated tests and architecture check

```sh
$DC run --rm sdk dotnet test GaussAuth.slnx
```

Expected: all tests pass, including the new session tests and the architecture test confirming Domain and Application have no reference to token or identity-model assemblies.
