# Quickstart: Release Validation Scenarios

Runnable validation guide for feature 012. Each scenario states prerequisites, commands and the expected outcome; results are recorded in `release-validation.md`. Contracts: [health](contracts/health-endpoints.md), [migrations](contracts/migration-command.md), [configuration](contracts/configuration-reference.md), [runtime](contracts/runtime-package.md). Per the constitution, .NET commands run inside the development SDK container; Docker commands run on the host Docker daemon.

## Prerequisites

- Development compose running (`postgres:17` healthy, `sdk` container) with a private `.env` (see `.env.example`).
- For image scenarios: Docker access to the host daemon.
- A throwaway signing key for evaluation only: `openssl ecparam -name prime256v1 -genkey -noout -out signing-key.pem` (never commit it).

## 1. Release build and full regression (US4, FR-001–005)

```bash
docker compose -f compose.dev.yml exec sdk dotnet restore
docker compose -f compose.dev.yml exec sdk dotnet build -c Release   # warnings are reviewed and classified per tasks T035, not blanket-promoted to errors
docker compose -f compose.dev.yml exec sdk dotnet test -c Release
```

Expected: zero errors; every warning in the correctness/security/nullability/lifetime/API-misuse categories fixed or justified in `release-validation.md`; all tests pass (existing suites for authentication, isolation, sessions, passwords, consumer auth, authorization contract, audit, profile files, administration, plus the new 012 tests).

## 2. Migrations from zero and upgrade with data (US2, FR-007–011)

Run by `releaseMigrationValidationTests` (throwaway uniquely named databases, dropped afterwards):

```bash
docker compose -f compose.dev.yml exec sdk dotnet test -c Release --filter "FullyQualifiedName~ReleaseMigrationValidation"
```

Expected: 10 migrations applied in order from an empty database; critical constraints/indexes present; API boots against it; upgrade test (state `…AddSecurityEvents` + data → latest) loses no rows and renames the audit permission with assignments intact; failing target yields non-zero exit and a value-free message.

## 3. SQL script equals migration command (US2, FR-012b)

```bash
docker build -t gaussauth:release .
docker create --name gx gaussauth:release && docker cp gx:/app/db/gaussauth-schema.sql ./schema.sql && docker rm gx
# DB A via command, DB B via script, both fresh databases on the dev postgres:
docker run --rm --network <dev-network> -e ConnectionStrings__AuthenticationDatabase="Host=postgres;Database=mig_a;Username=…;Password=…" gaussauth:release migrate
docker compose -f compose.dev.yml exec postgres psql -U "$POSTGRES_USER" -d mig_b -f - < schema.sql
# Compare:  pg_dump --schema-only mig_a  vs  mig_b ;  SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY 1
```

Expected: identical schema dump and identical history rows. The image contains no SDK (`docker run --rm --entrypoint dotnet gaussauth:release --list-sdks` lists none); no credentials in the script (`grep -i password schema.sql` empty).

## 4. Startup validation (US1, FR-014–021)

```bash
for missing in ConnectionStrings__AuthenticationDatabase Sessions__Signing__PrivateKeyPemFile ProfileImages__RootPath ProfileImages__StorageIsPersistent DataProtection__KeysPath; do
  # start image in Production with that one setting removed
done
```

Expected (also covered by `productionStartupValidationTests`): non-zero exit within seconds, a Critical log naming the setting key, no value printed, no stack trace; with every setting valid the service starts. Also verify: `DataProtection__KeysPath` relative or unwritable is refused naming only the key; Development/Testing start without it; `Staging` behaves as Production-class; `Development` still allows an ephemeral key; `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` without trust lists is refused; `ForwardedHeaders__KnownNetworks__0=0.0.0.0/0` is refused; `ProfileImages__StorageIsPersistent=false` starts with a visible Warning; with no recovery adapter the structured Warning appears; a reset credential issued before restarting the API container (key ring on the persistent volume) still validates after the restart, and does not when the volume is dropped (documented).

## 5. Health and readiness (US3)

```bash
curl -i localhost:8080/health/live   # 204
curl -i localhost:8080/health/ready  # 204
docker compose -f compose.release.yml stop postgres ; sleep 8 ; curl -i localhost:8080/health/live   # 204
curl -i localhost:8080/health/ready  # 503, empty body
docker compose -f compose.release.yml start postgres ; sleep 8 ; curl -i localhost:8080/health/ready  # 204
```

Expected: no body or dependency detail in any response; transition logged once. Repeat with the profile storage volume made unusable (`readinessTests` simulates it) and with a database one migration behind (pending migrations ⇒ 503).

## 6. First administrator bootstrap (US7, FR-044–046)

```bash
docker run --rm --network <net> -e ConnectionStrings__AuthenticationDatabase=… \
  -e Bootstrap__AdministratorEmail=admin@example.test -e Bootstrap__AdministratorPasswordFile=/run/secrets/bootstrap_pw \
  -v ./bootstrap_pw:/run/secrets/bootstrap_pw:ro gaussauth:release bootstrap-admin
```

Expected (`bootstrapAdministratorTests` + image run): prints a `UserId` and exit 0 on an empty database; the password is absent from output/logs; re-running exits non-zero with `users-exist` and writes nothing; with the list still empty a global operation is rejected; after setting `Administration__GlobalAdministratorUserIds__0=<UserId>` and restarting, the same user logs in and a global operation succeeds; the command refuses when users already exist even if the list is empty.

## 7. Recovery without a delivery channel (US1, FR-018a)

`recoveryWithoutDeliveryTests`: Production-class host, no adapter. Recovery for an existing and an unknown address returns identical responses; no reset credential is generated (spy on the credential service); no credential appears in logs, audit events or responses; Warning logged once at startup.

## 8. Edge/network baseline (US6, FR-030–035)

`httpBaselineTests`, `forwardedHeadersTests`, `rateLimitCoverageTests`:
- forced 500 returns the generic problem document with no stack trace, path, SQL or framework names; the exception is logged server-side with the correlation id;
- `X-Content-Type-Options: nosniff` present, no `Server` header, HSTS only on HTTPS requests in Production-class and never in Testing;
- simple and preflight cross-origin requests return no `Access-Control-*` headers;
- oversized body ⇒ 413 and oversized upload rejected at the configured limit;
- spoofed `X-Forwarded-For`/`-Proto` from an untrusted source changes neither client address nor scheme; from a configured trusted proxy they do (rate-limit bucket follows the forwarded client);
- every endpoint has a named rate-limit policy or is on the exempt list (health).

## 9. Image and compose (US5, FR-040–042)

```bash
cp .env.release.example .env.release   # replace placeholders locally; never commit
docker compose -f compose.release.yml --env-file .env.release up -d
docker compose -f compose.release.yml exec api id        # uid is not 0
docker inspect --format '{{.Config.User}}' gaussauth:release   # non-root user
docker history gaussauth:release | grep -i -E "password|secret|key" # nothing sensitive
```

Then: upload a profile image through the API, `docker compose … up -d --force-recreate api`, fetch the image again (must still exist), confirm `/health/ready` 204, run `docker stop` and confirm exit code 0 within the grace period and no partial files under `/data/profile-images`.
Failure path: break the connection string, `up` again — `migrate` exits non-zero and `api` never starts.

## 10. Secret hygiene (US7, FR-022)

```bash
scripts/scan-secrets.sh            # tracked files + git history
docker compose -f compose.dev.yml exec sdk dotnet test -c Release --filter "FullyQualifiedName~RepositorySecretHygiene"
```

Expected: zero findings other than allow-listed placeholders (`replace_with_*`, `not_a_secret`).

## 11. Consumer-secret and logging review (FR-024, FR-037)

Existing `consumerCredentialTests` plus log-capture assertions: per-application independence, hash-only storage, rotation current/previous, retirement, plaintext only in issuance/rotation response, and no secrets in logs/audit/errors across the whole run.

## 12. Documentation walk-through (US7, FR-043)

On a clean checkout follow `docs/deployment.md` → `docs/database.md` → `docs/configuration.md` to deploy, migrate, upgrade (previous image → new image), back up and restore (database + profile storage together), and rotate a consumer secret. Every Production-required key appears in `docs/configuration.md`; `docs/bootstrap.md` takes an empty database to a working global administrator; the documentation states ECDSA NIST P-256 for the signing key separately from the symmetric per-Application consumer secrets, that an empty administrator list means no global authority, and that key-ring persistence is required for reset credentials and other protected state; every item in `docs/limitations.md` is non-release-blocking.

## Done criteria

All scenarios pass or are recorded as fixed; `release-validation.md` has no open release-blocking finding; SC-001…SC-012 each have recorded evidence.
