# Quickstart Validation: Applications and Memberships

Use the existing Docker workflow only. This guide assumes a private `.env`
derived from `.env.example` and the existing feature-002 user API.

## 1. Start dependencies and apply the migration

```sh
docker compose --env-file .env -f compose.dev.yml up -d --wait postgres
docker compose --env-file .env -f compose.dev.yml run --rm sdk dotnet build GaussAuth.slnx
docker compose --env-file .env -f compose.dev.yml run --rm sdk sh -lc 'dotnet tool restore && dotnet ef database update --project src/GaussAuth.Infrastructure --startup-project src/GaussAuth.Api'
docker compose --env-file .env -f compose.dev.yml run --rm sdk sh -lc 'dotnet tool restore && dotnet ef migrations list --project src/GaussAuth.Infrastructure --startup-project src/GaussAuth.Api'
docker compose --env-file .env -f compose.dev.yml up -d sdk
```

Expected: PostgreSQL 17 is healthy, the migration is listed/applied, and the
API starts in the existing `sdk` container. Re-run the database-update command
to confirm migration application is idempotent.

## 2. Create a global user and two applications

Create a user with the existing contract, then keep its returned `id` as
`<USER_ID>`.

```sh
docker compose --env-file .env -f compose.dev.yml exec -T sdk curl -sS -i \
  -X POST http://127.0.0.1:8080/users \
  -H 'Content-Type: application/json' \
  -d '{"email":"member@example.test","password":"ValidPassword123!","firstName":"Member","lastName":"Example","displayName":"Member Example"}'

docker compose --env-file .env -f compose.dev.yml exec -T sdk curl -sS -i \
  -X POST http://127.0.0.1:8080/applications \
  -H 'Content-Type: application/json' \
  -d '{"code":"resto-manager","name":"Resto Manager"}'

docker compose --env-file .env -f compose.dev.yml exec -T sdk curl -sS -i \
  -X POST http://127.0.0.1:8080/applications \
  -H 'Content-Type: application/json' \
  -d '{"code":"payments-tracking","name":"Payments Tracking"}'
```

Expected: each creates one `201` record. Save the two application ids as
`<APP_A_ID>` and `<APP_B_ID>`. Repeating `resto-manager` returns `409`; a
lookup by id or `/applications/by-code/resto-manager` returns its original
record.

## 3. Create and inspect memberships

```sh
docker compose --env-file .env -f compose.dev.yml exec -T sdk curl -sS -i \
  -X POST http://127.0.0.1:8080/applications/<APP_A_ID>/memberships \
  -H 'Content-Type: application/json' -d '{"userId":"<USER_ID>"}'

docker compose --env-file .env -f compose.dev.yml exec -T sdk curl -sS -i \
  -X POST http://127.0.0.1:8080/applications/<APP_B_ID>/memberships \
  -H 'Content-Type: application/json' -d '{"userId":"<USER_ID>"}'

docker compose --env-file .env -f compose.dev.yml exec -T sdk curl -sS \
  http://127.0.0.1:8080/users/<USER_ID>/memberships
```

Expected: both creates return `201` and the user list contains two independent
active memberships. Repeating the first create returns `409`; querying a pair
with another application id never reports the first membership.

## 4. Verify independent lifecycle and inactive-parent policy

```sh
docker compose --env-file .env -f compose.dev.yml exec -T sdk curl -sS -i \
  -X POST http://127.0.0.1:8080/applications/<APP_A_ID>/memberships/<USER_ID>/deactivate

docker compose --env-file .env -f compose.dev.yml exec -T sdk curl -sS \
  http://127.0.0.1:8080/applications/<APP_B_ID>/memberships/<USER_ID>

docker compose --env-file .env -f compose.dev.yml exec -T sdk curl -sS -i \
  -X POST http://127.0.0.1:8080/applications/<APP_A_ID>/deactivate

docker compose --env-file .env -f compose.dev.yml exec -T sdk curl -sS -i \
  -X POST http://127.0.0.1:8080/applications/<APP_A_ID>/memberships/<USER_ID>/activate
```

Expected: deactivating the App A membership does not change App B. Deactivating
App A preserves App A's membership state. Activation while App A is inactive
returns `409`; reactivate App A before retrying activation successfully.

## 5. Run essential automated tests

```sh
docker compose --env-file .env -f compose.dev.yml run --rm sdk dotnet test GaussAuth.slnx
```

Expected: architecture, migration, application lifecycle/uniqueness,
membership uniqueness/concurrency, inactive-parent, and multi-application
isolation tests pass. Stop services when finished without global Docker
cleanup:

```sh
docker compose --env-file .env -f compose.dev.yml down
```
