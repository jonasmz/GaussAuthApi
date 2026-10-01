# Quickstart Validation: Authentication Login

Use the existing Docker workflow only. This guide assumes a private `.env` derived from `.env.example`, features 001-004 are applied, and the running API is available in the existing `sdk` service.

## 1. Build and run

```sh
docker compose --env-file .env -f compose.dev.yml up -d --wait postgres
docker compose --env-file .env -f compose.dev.yml run --rm sdk dotnet build GaussAuth.slnx
docker compose --env-file .env -f compose.dev.yml up -d sdk
```

Expected: build succeeds with no errors. No new migration is introduced by this feature (see `data-model.md`), so no `dotnet ef database update` step is required beyond what prior features already applied.

## 2. Create the prerequisite identity, application, and membership

Create an active global user, active application, and active membership through the existing routes from `002-users-profiles` and `003-applications-memberships`. Retain the user's email/password and the application's code as `<APP_CODE>`.

```sh
docker compose --env-file .env -f compose.dev.yml exec -T sdk curl -sS -i \
  -X POST http://127.0.0.1:8080/users \
  -H 'Content-Type: application/json' \
  -d '{"email":"quickstart-login@example.test","password":"Quickstart!2026","firstName":"A","lastName":"B","displayName":"AB"}'

docker compose --env-file .env -f compose.dev.yml exec -T sdk curl -sS -i \
  -X POST http://127.0.0.1:8080/applications \
  -H 'Content-Type: application/json' \
  -d '{"code":"<APP_CODE>","name":"Quickstart App"}'

docker compose --env-file .env -f compose.dev.yml exec -T sdk curl -sS -i \
  -X POST http://127.0.0.1:8080/applications/<APP_ID>/memberships \
  -H 'Content-Type: application/json' \
  -d '{"userId":"<USER_ID>"}'
```

## 3. Successful login

```sh
docker compose --env-file .env -f compose.dev.yml exec -T sdk curl -sS -i \
  -X POST http://127.0.0.1:8080/auth/login \
  -H 'Content-Type: application/json' \
  -d '{"applicationCode":"<APP_CODE>","email":"quickstart-login@example.test","password":"Quickstart!2026"}'
```

Expected: `200` with `{"userId":"...","applicationId":"..."}` matching the identifiers from step 2.

## 4. Uniform rejection for invalid credentials and unknown accounts

```sh
docker compose --env-file .env -f compose.dev.yml exec -T sdk curl -sS -i \
  -X POST http://127.0.0.1:8080/auth/login \
  -H 'Content-Type: application/json' \
  -d '{"applicationCode":"<APP_CODE>","email":"quickstart-login@example.test","password":"WrongPassword!"}'

docker compose --env-file .env -f compose.dev.yml exec -T sdk curl -sS -i \
  -X POST http://127.0.0.1:8080/auth/login \
  -H 'Content-Type: application/json' \
  -d '{"applicationCode":"<APP_CODE>","email":"no-such-user@example.test","password":"Quickstart!2026"}'
```

Expected: both return the same `401` Problem Details body/status — a wrong password and an unknown email are indistinguishable externally.

## 5. State boundaries

```sh
# Deactivate the user, then retry the originally correct login.
docker compose --env-file .env -f compose.dev.yml exec -T sdk curl -sS -i \
  -X POST http://127.0.0.1:8080/users/<USER_ID>/deactivate

docker compose --env-file .env -f compose.dev.yml exec -T sdk curl -sS -i \
  -X POST http://127.0.0.1:8080/auth/login \
  -H 'Content-Type: application/json' \
  -d '{"applicationCode":"<APP_CODE>","email":"quickstart-login@example.test","password":"Quickstart!2026"}'

docker compose --env-file .env -f compose.dev.yml exec -T sdk curl -sS -i \
  -X POST http://127.0.0.1:8080/users/<USER_ID>/activate
```

Expected: `401` while the user is inactive, using the same uniform shape as step 4. Repeat similarly for an inactive application and for a missing/inactive membership (reusing the deactivate routes from `003-applications-memberships`); each independently produces the same `401`.

## 6. Lockout and rate limiting

```sh
# Repeat an incorrect password beyond the configured MaxFailedAccessAttempts.
for i in 1 2 3 4 5 6; do
  docker compose --env-file .env -f compose.dev.yml exec -T sdk curl -sS -o /dev/null -w '%{http_code}\n' \
    -X POST http://127.0.0.1:8080/auth/login \
    -H 'Content-Type: application/json' \
    -d '{"applicationCode":"<APP_CODE>","email":"quickstart-login@example.test","password":"WrongPassword!"}'
done

# Then retry with the CORRECT password while locked out.
docker compose --env-file .env -f compose.dev.yml exec -T sdk curl -sS -i \
  -X POST http://127.0.0.1:8080/auth/login \
  -H 'Content-Type: application/json' \
  -d '{"applicationCode":"<APP_CODE>","email":"quickstart-login@example.test","password":"Quickstart!2026"}'
```

Expected: once the configured lockout threshold is reached, even the correct password returns the same `401` uniform rejection used throughout.

```sh
# Exceed the configured login rate limit from one source within one window.
for i in $(seq 1 10); do
  docker compose --env-file .env -f compose.dev.yml exec -T sdk curl -sS -o /dev/null -w '%{http_code}\n' \
    -X POST http://127.0.0.1:8080/auth/login \
    -H 'Content-Type: application/json' \
    -d '{"applicationCode":"<APP_CODE>","email":"quickstart-login@example.test","password":"Quickstart!2026"}'
done
```

Expected: once the configured `login` rate-limit window is exceeded, later requests in the same window return `429` with a `Retry-After` header, distinct from the `401` shape.

## 7. Run essential automated tests

```sh
docker compose --env-file .env -f compose.dev.yml run --rm sdk dotnet test GaussAuth.slnx
```

Expected: successful login, invalid password, unknown email, inactive user, inactive application, missing membership, inactive membership, locked-out user, rate limiting, normalized-email login, absence of credential leakage, and Domain/Application independence from Identity infrastructure tests all pass.
