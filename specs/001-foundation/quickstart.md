# Quickstart Validation: 001-foundation

This is a run guide for the planned implementation. It becomes executable
after the files named in [plan.md](plan.md) are built by the implementation
phase. Run every command from the repository root inside the existing
Codex development container. No host .NET or PostgreSQL installation is
needed. The SDK and database run in separate containers.

## Prerequisites

- Docker CLI and Compose can reach the host daemon through the mounted socket.
- `compose.dev.yml`, `.env.example`, and the .NET 10 solution have been
  created by implementation.
- Use a private, development-only password in ignored `.env`; never copy
  it into source, logs, screenshots, or command output.

## 1. Check Docker and configure development settings

```sh
test -S /var/run/docker.sock
docker info --format '{{.ServerVersion}}'
cp .env.example .env
```

Fill the placeholder development values in `.env`, including the database
password. The implementation must ignore `.env` in Git and validate missing
values without printing them. The development container itself is unchanged.

## 2. Start and inspect PostgreSQL 17

```sh
docker compose --env-file .env -f compose.dev.yml up -d postgres
docker compose --env-file .env -f compose.dev.yml ps
docker compose --env-file .env -f compose.dev.yml run --rm sdk dotnet --version
```

Expected: `postgres` reports healthy, its image is PostgreSQL 17, and the
separate SDK container reports .NET 10. No host database port needs to be
published when the SDK and database share the Compose network. If the
daemon/socket is unavailable, stop here and report that prerequisite; do
not fall back to a host-installed database.

## 3. Build and apply the initial migration

```sh
docker compose --env-file .env -f compose.dev.yml run --rm sdk dotnet build GaussAuth.slnx
docker compose --env-file .env -f compose.dev.yml run --rm sdk sh -lc 'dotnet tool restore && dotnet ef database update --project src/GaussAuth.Infrastructure --startup-project src/GaussAuth.Api'
docker compose --env-file .env -f compose.dev.yml run --rm sdk sh -lc 'dotnet tool restore && dotnet ef migrations list --project src/GaussAuth.Infrastructure --startup-project src/GaussAuth.Api'
```

Expected: build succeeds; the single initial migration is applied and listed.
Repeat the `database update` command; it must complete without adding a
second schema change. The migration should contain only the user-side
Identity foundation described in [data-model.md](data-model.md).
The migration and model snapshot are version-controlled.

## 4. Start the API and check its only route

```sh
docker compose --env-file .env -f compose.dev.yml up -d sdk
docker compose --env-file .env -f compose.dev.yml ps
docker compose --env-file .env -f compose.dev.yml exec -T sdk curl -sS -o /dev/null -w '%{http_code}\n' http://127.0.0.1:8080/health/live
```

Expected: the SDK service runs the API and the final command prints `204`.
The implementation must verify the selected SDK image provides the HTTP
client used here, or update this guide to the actual in-container client.
The response body is empty. The route does not query PostgreSQL or disclose
configuration. The API must expose no identity or business endpoint.

## 5. Run essential checks

```sh
docker compose --env-file .env -f compose.dev.yml run --rm sdk dotnet test GaussAuth.slnx
```

Expected: architecture, startup/error, and persistence checks pass. Review
the failure-path test output: production responses and logs must contain
no SQL, connection string, secret, stack trace, or physical path. Check
that Domain and Application have no forbidden project/package references
and that source files follow the constitutional one-type and filename rules.

## 6. Stop or recreate intentionally

```sh
docker compose --env-file .env -f compose.dev.yml down
```

This stops and removes the development service containers while preserving
the named PostgreSQL volume. To deliberately erase disposable development
data before a fresh validation, run:

```sh
docker compose --env-file .env -f compose.dev.yml down -v
docker compose --env-file .env -f compose.dev.yml up -d postgres
```

Use `down -v` only when losing local development data is intended. No
obsolete temporary containers should remain after the workflow.
