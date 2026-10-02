# Contract: Production Runtime Package

## Image

| Aspect | Contract |
|--------|----------|
| Build | Multi-stage `Dockerfile` at repo root; `sdk:10.0` build/publish stage, `aspnet:10.0` runtime stage. Release configuration. Reproducible from a clean checkout with `docker build`. |
| User | Runs as the non-root built-in `app` user. |
| Entrypoint | Exec form `["dotnet","GaussAuth.Api.dll"]` so the process receives SIGTERM directly. No shell wrapper. |
| Environment defaults | `ASPNETCORE_ENVIRONMENT=Production`, `ASPNETCORE_HTTP_PORTS=8080`. **Must not** set `ASPNETCORE_FORWARDEDHEADERS_ENABLED`. |
| Contents | Published application, `/app/db/gaussauth-schema.sql`. **No** SDK, source, tests, `.env`, keys, passwords, consumer secrets, or development tooling. |
| Writable paths | `/data/profile-images`, `/data/keys` — created and owned by `app`; declared as volumes. Everything else read-only capable. |
| Ports | 8080 (HTTP). TLS terminates upstream (proxy/ingress). |
| Commands | no args → API; `migrate` → migration command. |

## Evaluation composition (`compose.release.yml`)

| Service | Role |
|---------|------|
| `postgres` | `postgres:17`, named volume, healthcheck (`pg_isready`). |
| `migrate` | same API image, `command: ["migrate"]`, one-shot, depends on healthy `postgres`. |
| `api` | API image; `depends_on: migrate: service_completed_successfully`; port 8080 published to localhost; volumes for profile images and key ring; `stop_grace_period` greater than the host shutdown timeout. |

Configuration comes from `.env.release` (git-ignored; template `.env.release.example` has `replace_with_*` placeholders only). The signing key is a mounted file referenced by `Sessions__Signing__PrivateKeyPemFile`. Required Production settings set explicitly: connection string, signing key file, `ProfileImages__RootPath=/data/profile-images`, `ProfileImages__StorageIsPersistent=true`, `DataProtection__KeysPath=/data/keys`. No trusted proxy is configured by default (direct evaluation; the documented warning appears).

`compose.dev.yml` and the AI development container are not modified; the development container is not the production image.

## Shutdown contract

`docker stop` → SIGTERM → host stops accepting requests, drains in-flight requests up to the host shutdown timeout (30 s), cancels long operations via request cancellation tokens, exits 0. Compose grace period > 30 s so the runtime is never SIGKILLed mid-drain.

## Acceptance checks (see quickstart)

non-root user; no secrets in image layers or history; SkiaSharp loads; migrate-then-start ordering and failure stop; avatar survives API container replacement; readiness 204; SIGTERM exits cleanly with no partial files.
