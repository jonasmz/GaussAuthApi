# Quickstart Validation: Roles and Permissions

Use the existing Docker workflow only. This guide assumes a private `.env` derived from `.env.example`, feature 003 is applied, and the running API is available in the existing `sdk` service.

## 1. Apply and verify the migration

```sh
docker compose --env-file .env -f compose.dev.yml up -d --wait postgres
docker compose --env-file .env -f compose.dev.yml run --rm sdk dotnet build GaussAuth.slnx
docker compose --env-file .env -f compose.dev.yml run --rm sdk sh -lc 'dotnet tool restore && dotnet ef database update --project src/GaussAuth.Infrastructure --startup-project src/GaussAuth.Api'
docker compose --env-file .env -f compose.dev.yml run --rm sdk sh -lc 'dotnet tool restore && dotnet ef migrations list --project src/GaussAuth.Infrastructure --startup-project src/GaussAuth.Api'
docker compose --env-file .env -f compose.dev.yml up -d sdk
```

Expected: the authorization migration is listed and applied. Re-run database update to verify idempotence.

## 2. Create application context and authorization vocabulary

Create an active global user, active application, and active membership through the existing routes. Retain their identifiers as `<USER_ID>` and `<APP_ID>`.

```sh
docker compose --env-file .env -f compose.dev.yml exec -T sdk curl -sS -i \
  -X POST http://127.0.0.1:8080/applications/<APP_ID>/roles \
  -H 'Content-Type: application/json' \
  -d '{"name":"Operator","description":"Operations"}'

docker compose --env-file .env -f compose.dev.yml exec -T sdk curl -sS -i \
  -X POST http://127.0.0.1:8080/applications/<APP_ID>/permissions \
  -H 'Content-Type: application/json' \
  -d '{"code":"reservations.read","description":"Read reservations"}'
```

Expected: both calls return `201`. Save the identifiers as `<ROLE_ID>` and `<PERMISSION_ID>`. Repeating the role with different letter case or repeating the permission code returns `409`; creating the same values in another application succeeds.

## 3. Compose and assign authorization

```sh
docker compose --env-file .env -f compose.dev.yml exec -T sdk curl -sS -i \
  -X POST http://127.0.0.1:8080/applications/<APP_ID>/roles/<ROLE_ID>/permissions/<PERMISSION_ID>

docker compose --env-file .env -f compose.dev.yml exec -T sdk curl -sS -i \
  -X POST http://127.0.0.1:8080/applications/<APP_ID>/users/<USER_ID>/roles/<ROLE_ID>

docker compose --env-file .env -f compose.dev.yml exec -T sdk curl -sS \
  http://127.0.0.1:8080/applications/<APP_ID>/users/<USER_ID>/effective-permissions
```

Expected: the relationships are active and effective permissions include `reservations.read` once. A role/permission or user/role combination from another application returns `409`; a user without active membership cannot receive the role.

## 4. Verify lifecycle, history, and isolation

```sh
docker compose --env-file .env -f compose.dev.yml exec -T sdk curl -sS -i \
  -X POST http://127.0.0.1:8080/applications/<APP_ID>/users/<USER_ID>/roles/<ROLE_ID>/remove

docker compose --env-file .env -f compose.dev.yml exec -T sdk curl -sS \
  http://127.0.0.1:8080/applications/<APP_ID>/users/<USER_ID>/effective-permissions

docker compose --env-file .env -f compose.dev.yml exec -T sdk curl -sS -i \
  -X POST http://127.0.0.1:8080/applications/<APP_ID>/users/<USER_ID>/roles/<ROLE_ID>

docker compose --env-file .env -f compose.dev.yml exec -T sdk curl -sS -i \
  -X POST http://127.0.0.1:8080/applications/<APP_ID>/permissions/<PERMISSION_ID>/deactivate
```

Expected: removal yields no effective permission but preserves the relationship; the valid repeated assignment reactivates the same relationship. Deactivating the permission removes it from effective permissions without deleting history. Repeat with a second application to confirm no authorization data leaks between contexts.

## 5. Run essential automated tests

```sh
docker compose --env-file .env -f compose.dev.yml run --rm sdk dotnet test GaussAuth.slnx
```

Expected: architecture, migration, uniqueness, active-state, relationship reactivation, cross-application isolation, and effective-permission de-duplication tests pass.
