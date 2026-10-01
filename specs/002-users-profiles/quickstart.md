# Quickstart Validation: 002-users-profiles

This is a run guide for the planned implementation. It becomes executable
after the files named in [plan.md](plan.md) are built by the implementation
phase. It reuses the exact development environment established by
`001-foundation` — `compose.dev.yml`'s `postgres` and `sdk` services — with
no new container. Run every command from the repository root inside the
existing development container; see
[001-foundation/quickstart.md](../001-foundation/quickstart.md) for Docker
socket/`.env` prerequisites, which are unchanged.

## 1. Start the reused development services

```sh
docker compose --env-file .env -f compose.dev.yml up -d --wait postgres
```

Expected: `postgres` reports healthy, as in `001-foundation`. No new service
is introduced by this feature.

## 2. Build and apply the new migration

```sh
docker compose --env-file .env -f compose.dev.yml run --rm sdk dotnet build GaussAuth.slnx
docker compose --env-file .env -f compose.dev.yml run --rm sdk sh -lc 'dotnet tool restore && dotnet ef database update --project src/GaussAuth.Infrastructure --startup-project src/GaussAuth.Api'
docker compose --env-file .env -f compose.dev.yml run --rm sdk sh -lc 'dotnet tool restore && dotnet ef migrations list --project src/GaussAuth.Infrastructure --startup-project src/GaussAuth.Api'
```

Expected: build succeeds; two migrations are now listed (the
`001-foundation` initial migration plus this feature's new migration adding
`Users`/`UserProfiles`, per [data-model.md](data-model.md)). Repeat the
`database update` command; it must complete without adding a further schema
change.

## 3. Start the API

```sh
docker compose --env-file .env -f compose.dev.yml up -d sdk
docker compose --env-file .env -f compose.dev.yml logs -f sdk
# Wait for "Now listening on: http://0.0.0.0:8080", then Ctrl+C.
```

## 4. Exercise the user lifecycle (see contracts/users-api.md for exact shapes)

```sh
# Create a user; capture its id from the Location header / response body.
docker compose --env-file .env -f compose.dev.yml exec -T sdk curl -sS -i \
  -X POST http://127.0.0.1:8080/users \
  -H 'Content-Type: application/json' \
  -d '{"email":"quickstart@example.dev","password":"Quickstart!2026","firstName":"Quick","lastName":"Start","displayName":"Quick Start"}'

# Re-submit the same email: expect 409 Conflict (duplicate normalized email).
docker compose --env-file .env -f compose.dev.yml exec -T sdk curl -sS -o /dev/null -w '%{http_code}\n' \
  -X POST http://127.0.0.1:8080/users \
  -H 'Content-Type: application/json' \
  -d '{"email":"Quickstart@example.dev ","password":"Quickstart!2026","firstName":"Quick","lastName":"Start","displayName":"Quick Start"}'

# Retrieve the created user by id (replace USER_ID).
docker compose --env-file .env -f compose.dev.yml exec -T sdk curl -sS \
  http://127.0.0.1:8080/users/USER_ID

# Update permitted profile fields.
docker compose --env-file .env -f compose.dev.yml exec -T sdk curl -sS -i \
  -X PUT http://127.0.0.1:8080/users/USER_ID/profile \
  -H 'Content-Type: application/json' \
  -d '{"firstName":"Quick","lastName":"Start","displayName":"Q. Start","phoneNumber":"+15550000000"}'

# Deactivate, then confirm idempotent repeat, then reactivate.
docker compose --env-file .env -f compose.dev.yml exec -T sdk curl -sS -o /dev/null -w '%{http_code}\n' \
  -X POST http://127.0.0.1:8080/users/USER_ID/deactivate
docker compose --env-file .env -f compose.dev.yml exec -T sdk curl -sS -o /dev/null -w '%{http_code}\n' \
  -X POST http://127.0.0.1:8080/users/USER_ID/deactivate
docker compose --env-file .env -f compose.dev.yml exec -T sdk curl -sS -o /dev/null -w '%{http_code}\n' \
  -X POST http://127.0.0.1:8080/users/USER_ID/activate
```

```sh
# Exceed the default rate limit (5 requests/60s) on POST /users with distinct emails.
for i in 1 2 3 4 5 6; do
  docker compose --env-file .env -f compose.dev.yml exec -T sdk curl -sS -o /dev/null -w '%{http_code}\n' \
    -X POST http://127.0.0.1:8080/users \
    -H 'Content-Type: application/json' \
    -d "{\"email\":\"rate-limit-$i@example.dev\",\"password\":\"Quickstart!2026\",\"firstName\":\"Quick\",\"lastName\":\"Start\",\"displayName\":\"Quick Start\"}"
done
```

Expected: the first five requests above succeed (`201`, counting against the
same window as the earlier creation/duplicate calls), and the 6th returns
`429 Too Many Requests` with a `Retry-After` header (FR-024). Wait for the
configured window to elapse before creating further users in this guide.

Expected: `201` on first creation with a `Location` header and a body
containing no `password`/credential field; `409` on the duplicate-email
re-submission (case/whitespace difference must still collide); `200` with
the current `UserResponse` on retrieval and profile update; `200` (not
`409`/`404`) on both deactivate calls, the second one returning the same
`isActive: false` state without changing `updatedAt`; `200` on reactivation.

## 5. Run essential checks

```sh
docker compose --env-file .env -f compose.dev.yml run --rm sdk dotnet test GaussAuth.slnx
```

Expected: all `001-foundation` architecture/startup/migration tests still
pass unchanged, plus this feature's new tests (user creation, duplicate
rejection, retrieval, profile update, activation/deactivation, persistence).
Review failure-path output: no password, password hash, security stamp, or
SQL ever appears.

## 6. Stop or recreate intentionally

```sh
docker compose --env-file .env -f compose.dev.yml down
```

Same lifecycle command as `001-foundation`; see that feature's quickstart
for the `down -v` clean-recreate variant.
