---

description: "Task list for feature 012-hardening-release"
---

# Tasks: Hardening and Release Readiness

**Input**: Design documents from `/specs/012-hardening-release/`

**Prerequisites**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md), [data-model.md](data-model.md), [contracts/](contracts/), [quickstart.md](quickstart.md)

**Tests**: Included. This is a release-validation feature and the plan lists the focused tests required for the release-blocking categories. No coverage-padding tests: every test task below maps to a spec requirement or a release-blocking category.

**Organization**: Tasks are grouped by user story (US1–US8 from spec.md) so each story is independently implementable and verifiable.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: parallelizable (different files, no dependency on an incomplete task)
- **[Story]**: US1–US8 for user-story phases only
- Every task names its file(s). Per the constitution: exactly one top-level type per C# file, named `<name>.<type>.cs` (existing suffixes are reused — `service`, `extension`, `options`, `adapter`, `handler`, `fixture`, `test` — plus two deliberate additions with no existing equivalent: `exception` and `check`).
- Supporting types never share a file with their owner: options validators go in `<name>.validator.cs`, result/outcome types in `<name>.result.cs` (both are existing suffixes), keeping one top-level type per file.
- .NET commands run inside the development SDK container (`docker compose -f compose.dev.yml exec sdk dotnet …`); Docker image commands run on the host Docker daemon. Do not run implementation through spawned agents.
- `src/GaussAuth.Api/Program.cs` is edited by several tasks; those tasks are intentionally **not** marked [P] and must run in ID order.

## Path Conventions

Four-project solution: `src/GaussAuth.Domain`, `src/GaussAuth.Application`, `src/GaussAuth.Infrastructure`, `src/GaussAuth.Api`; tests in `tests/GaussAuth.Foundation.Tests`. Operator material in `docs/` and `scripts/`; image/compose files at the repository root.

---

## Phase 1: Setup

- [X] T001 Capture the pre-change baseline in `specs/012-hardening-release/release-validation.md` (create the file from the section layout in `data-model.md` → *Release Validation Record*): run `dotnet restore`, `dotnet build -c Release` and `dotnet test -c Release` in the SDK container against the dev PostgreSQL 17 and record SDK version, warning list and pass/fail counts (FR-001)
- [X] T002 Run `dotnet tool restore` in the SDK container and confirm `dotnet ef --version` reports 10.0.12 (pinned in `.config/dotnet-tools.json`); record the result in `release-validation.md`
- [X] T003 [P] Add the *Findings* table (id, area, release-blocking yes/no with category, disposition) and seed it with the four pre-identified findings: recovery credential generated without a channel (R9), scattered/opaque startup failures (R2), no readiness endpoint (R5), and the unbootstrappable first administrator (R22) in `specs/012-hardening-release/release-validation.md`

---

## Phase 2: Foundational (blocking prerequisites)

**Purpose**: shared building blocks every story uses. No user story work starts before this phase is complete.

- [X] T004 [P] Create `StartupConfigurationException` (carries a setting key and a value-free reason; `Message` is `<key>: <reason>`; never accepts a value parameter) in `src/GaussAuth.Infrastructure/Configuration/startupConfigurationException.exception.cs`
- [X] T005 [P] Create the Production-class rule `IsProductionClass(this IHostEnvironment)` — true unless the environment is `Development` or `Testing` (research R1) — in `src/GaussAuth.Infrastructure/Configuration/productionClassEnvironment.extension.cs`
- [X] T006 [P] Create a throwaway-database test fixture that connects to the maintenance database of the test PostgreSQL 17 server, creates a uniquely named empty database, exposes its connection string, and drops it on dispose (terminating connections), in `tests/GaussAuth.Foundation.Tests/throwawayDatabase.fixture.cs`
- [X] T007 [P] Create a process-based API launcher helper (start the built API assembly with given environment variables, capture stdout/stderr, enforce a timeout, kill on hang) by extracting the logic already in `tests/GaussAuth.Foundation.Tests/startupTests.test.cs` into `tests/GaussAuth.Foundation.Tests/apiProcess.fixture.cs`; keep `startupTests.test.cs` behavior unchanged
- [X] T008 Introduce the command-line dispatcher: add `CommandLineCommands.TryRunAsync(string[] args)` in `src/GaussAuth.Api/Commands/commandLine.service.cs` that recognizes `migrate` and `bootstrap-admin` (handlers added in US2/US7) and returns the exit code, and call it first in `src/GaussAuth.Api/Program.cs` so that no arguments leaves today's web-host path untouched (`public partial class Program` in `src/GaussAuth.Api/program.entrypoint.cs` must keep working with `WebApplicationFactory<Program>`)
- [X] T009 Add startup failure handling in `src/GaussAuth.Api/Program.cs`: wrap service registration (`AddApplication`/`AddInfrastructure`/`AddApiServices`, where the eager configuration loaders throw), `builder.Build()` and `app.RunAsync()` (where `ValidateOnStart` options failures surface during host start) in one `try`; catch **only** `StartupConfigurationException` and `OptionsValidationException`, delegating to `src/GaussAuth.Api/Startup/startupFailureReporter.service.cs` (`WebApplicationFactory` flow preserved), which writes one Critical structured log per offending setting key with a value-free reason and exits non-zero with **no stack trace** and no secret values (FR-014, FR-016)

**Checkpoint**: foundation ready — user stories can begin.

---

## Phase 3: User Story 1 — Safe and Predictable Startup (Priority: P1) 🎯 MVP

**Goal**: Production refuses to start on missing/invalid critical configuration (naming only the setting, never a value) and never silently starts with development conveniences; password recovery behaves safely without a delivery channel.

**Independent Test**: start the API as `Production` (and `Staging`) with each critical setting removed/invalid in turn and observe fail-fast with a key-naming, value-free message; start with a complete valid configuration and observe a normal start with the documented warnings.

- [X] T010 [P] [US1] Write `productionStartupValidationTests` in `tests/GaussAuth.Foundation.Tests/productionStartupValidationTests.test.cs` using the process launcher: for `Production` **and** `Staging`, each of `ConnectionStrings__AuthenticationDatabase`, `Sessions__Signing__PrivateKeyPem(File)`, `ProfileImages__RootPath`, `ProfileImages__StorageIsPersistent`, `DataProtection__KeysPath` missing/invalid ⇒ exit ≠ 0 within seconds, output names the key, contains none of the supplied secret values and no stack trace; invalid lifetimes (access > session, ≤ 0), impossible rate limits, malformed global-administrator id and legacy `SecurityAudit__GlobalReviewerUserId` ⇒ refused; `Development` and `Testing` may start without the key-ring path, and **only `Development`** may start without a signing key (ephemeral key; `Testing` supplies one, as today); `ProfileImages__StorageIsPersistent=false` starts with the non-persistent warning (FR-014–019, FR-041, FR-041a, SC-004, SC-005)
- [X] T011 [US1] Apply the Production-class rule and `StartupConfigurationException` naming `Sessions:Signing:PrivateKeyPem` / `Sessions:Signing:PrivateKeyPemFile` in `src/GaussAuth.Infrastructure/Sessions/accessCredentialSigningKey.service.cs` (no ephemeral/dev key outside `Development`; keep the 256-bit EC/PEM validation; never echo PEM content or file contents)
- [X] T012 [US1] Replace the inline connection-string, session-lifetime and lockout checks with `StartupConfigurationException` messages that name `ConnectionStrings:AuthenticationDatabase`, `Sessions:SessionLifetimeMinutes`, `Sessions:AccessTokenLifetimeMinutes`, `Identity:Lockout:*` (values > 0, access ≤ session) in `src/GaussAuth.Infrastructure/DependencyInjection/infrastructureServiceCollectionExtensions.extension.cs`
- [X] T013 [P] [US1] Switch `Administration:GlobalAdministratorUserIds` / legacy-key errors and `SecurityAudit:RetentionDays` errors to `StartupConfigurationException` naming the key (blank entries still ignored; empty list ⇒ no global authority, unchanged) in `src/GaussAuth.Infrastructure/Administration/configuredGlobalAdministratorPolicy.service.cs` and `src/GaussAuth.Infrastructure/Security/securityAuditRetentionPolicy.options.cs`
- [X] T014 [P] [US1] Update `ProfileImagesOptions.Load` in `src/GaussAuth.Infrastructure/ProfileImages/profileImagesOptions.options.cs`: use `IsProductionClass`; require `ProfileImages:StorageIsPersistent` in Production-class (missing ⇒ `StartupConfigurationException` naming the key; `true` ⇒ normal; `false` ⇒ start flagged for a Warning); add opt-in `ProfileImages:RequirePersistenceDeclaration` for other environments; never infer persistence from the path; keep `RootPath` absolute/no-NUL rules and expose the declaration for the startup diagnostics
- [X] T015 [P] [US1] Create `KeyRingOptions` (named to avoid clashing with the framework's `DataProtectionOptions`) in `src/GaussAuth.Infrastructure/Configuration/keyRingOptions.options.cs` (validation in `keyRingOptions.validator.cs`): read `DataProtection:KeysPath`; **required in Production-class**, optional in Development/Testing; must be absolute, free of NUL, and creatable/writable at startup, otherwise `StartupConfigurationException` naming only `DataProtection:KeysPath` (no path contents or key material in messages) (FR-041a, research R11)
- [X] T016 [US1] Wire Data Protection in `src/GaussAuth.Infrastructure/DependencyInjection/infrastructureServiceCollectionExtensions.extension.cs`: `AddDataProtection().SetApplicationName("GaussAuth")` plus `PersistKeysToFileSystem` when a path is configured; register the loaded `KeyRingOptions` and `ProfileImagesOptions` declaration for diagnostics (depends on the two tasks above and on the connection/session task editing the same file)
- [X] T017 [P] [US1] Create validated per-group rate-limit options (`PermitLimit` 1 … 10 000 000 and `WindowSeconds` 1 … 86 400 — the upper bound must stay ≥ the 1 000 000 permit limit the existing administrative test host sets) for Login, PasswordRecovery, PasswordReset, UserCreation, AuthorizationContext, SessionCredentials, SigningKeys, SecurityEvents, ProfileImageWrite and Administration with the **existing defaults** (see `contracts/configuration-reference.md`) in `src/GaussAuth.Api/DependencyInjection/rateLimitOptions.options.cs` (validator in `rateLimitOptions.validator.cs`), loaded eagerly at service registration through the value-free `ConfigurationValueReader` in `src/GaussAuth.Infrastructure/Configuration/configurationValueReader.extension.cs` (the framework binder is avoided because its conversion errors can echo the configured value; the group type lives in `rateLimitPolicy.options.cs`)
- [X] T018 [US1] Replace the inline `GetValue` block in `src/GaussAuth.Api/DependencyInjection/apiServiceCollectionExtensions.extension.cs` with the validated rate-limit options; keep every policy name, partition key and default value unchanged so existing behavior and tests are preserved (depends on the options task)
- [X] T019 [US1] Add `bool IsAvailable { get; }` to `src/GaussAuth.Application/Passwords/Ports/recoveryDelivery.interface.cs`; implement it in `src/GaussAuth.Infrastructure/Passwords/protectedFileRecoveryDelivery.service.cs` (true only in `Development`/`Testing` with a configured `PasswordRecovery:DeliveryFile`; never true in Production-class); in `src/GaussAuth.Application/Passwords/passwordManagementService.service.cs` make `RequestRecoveryAsync` return **before** `GenerateResetCredentialAsync` when delivery is unavailable so no credential is generated, logged, audited or returned and the response stays identical for existing and unknown accounts (FR-018a, research R9)
- [X] T020 [P] [US1] Write `recoveryWithoutDeliveryTests` in `tests/GaussAuth.Foundation.Tests/recoveryWithoutDeliveryTests.test.cs`: Production-class host without an adapter — recovery for an existing and an unknown address returns identical status/body, the credential service is never asked to generate a credential (spy), no reset credential or token text appears in logs, security events or responses; with an adapter available the existing flow is unchanged
- [X] T021 [US1] Create the startup diagnostics hosted service in `src/GaussAuth.Infrastructure/Configuration/startupDiagnostics.service.cs`: on start log one structured Information summary (environment, class, non-sensitive settings only — never secrets, paths to key files, or connection strings), emit the Production-class warnings (storage declared non-persistent; recovery delivery not configured; framework key-ring-not-encrypted note documented; the no-trusted-proxy warning is emitted by the forwarded-headers setup task in US6, not here), and log shutdown start/stop (FR-021); register it in `src/GaussAuth.Infrastructure/DependencyInjection/infrastructureServiceCollectionExtensions.extension.cs` and add the request-body-limit key naming (`RequestLimits:MaxBodyBytes`) via `StartupConfigurationException` in `src/GaussAuth.Api/Program.cs`
- [X] T022 [US1] Update the existing startup/profile/recovery tests that assume the old messages or the old recovery-in-Production behavior (`tests/GaussAuth.Foundation.Tests/startupTests.test.cs`, `tests/GaussAuth.Foundation.Tests/passwordManagementTests.test.cs`, `tests/GaussAuth.Foundation.Tests/profileImageTests.test.cs`) so the suite is green with the new key-naming messages; do not weaken any assertion

**Checkpoint**: US1 independently verifiable — Production start/fail behavior and recovery-without-channel are proven.

---

## Phase 4: User Story 2 — Reproducible Database Creation and Upgrade (Priority: P1)

**Goal**: a database is created from zero and upgraded from the previous release state by an explicit, documented mechanism with no silent data loss; the running service never migrates.

**Independent Test**: apply the full chain to an empty PostgreSQL 17 database and boot the API against it; migrate a database at the `…AddSecurityEvents` state with representative data to the latest; prove no data loss and an actionable failure diagnostic.

- [X] T023 [P] [US2] Create `DatabaseMigrator` in `src/GaussAuth.Infrastructure/Persistence/databaseMigrator.service.cs` (outcome type in `databaseMigration.result.cs`): applies pending migrations in order via the EF Core migrator, logs each applied migration id, returns a result classifying failures as `connection`, `authentication`, `migration-failed` or `configuration` with the failing migration id when known; never includes the connection string, password or stack trace in messages
- [X] T024 [US2] Implement the `migrate` command host in `src/GaussAuth.Api/Commands/migrateCommand.service.cs`: a minimal host registering only the database context (needs only `ConnectionStrings:AuthenticationDatabase`; no signing key/storage settings), runs `DatabaseMigrator`, exit code 0 on success/no-op and ≠ 0 on failure with one structured Critical log; register it in `src/GaussAuth.Api/Commands/commandLine.service.cs`; the web host must still never call `Migrate`; the migrator also takes a PostgreSQL advisory lock for the whole run (found necessary by T030) and reports category `locked` on timeout (FR-012, FR-012a, FR-012c; `contracts/migration-command.md`)
- [X] T025 [P] [US2] Write `releaseMigrationValidationTests` (from zero) in `tests/GaussAuth.Foundation.Tests/releaseMigrationValidationTests.test.cs` using the throwaway-database fixture: all 10 migrations applied in order; re-run is a no-op; critical constraints/indexes exist (unique normalized email, unique application code, unique membership per user+application, application-scoped unique role name / permission code, role-permission and user-role pair uniqueness, one consumer credential per application, session and security-event lookup indexes — take exact names from `AuthenticationDbContextModelSnapshot`); the API boots and a request succeeds against the resulting schema (FR-007, FR-008, SC-002)
- [X] T026 [P] [US2] Write the upgrade-with-data test in `tests/GaussAuth.Foundation.Tests/releaseMigrationUpgradeTests.test.cs`: migrate a throwaway database only to `20261001202342_AddSecurityEvents`, seed representative users, applications, memberships, roles, the pre-rename audit permission with role assignments, sessions and security events, migrate to the latest, then assert zero row loss in each table, the audit permission is renamed to `auth.security.audit.read` with assignments intact, seeded administrative permissions are present exactly once, and the resulting schema (tables, columns, indexes, constraints from `information_schema`/`pg_indexes`) equals the schema of a database migrated from zero (FR-009, FR-010, SC-003)
- [X] T027 [P] [US2] Write `migrateCommandTests` in `tests/GaussAuth.Foundation.Tests/migrateCommandTests.test.cs` using the process launcher: success on an empty throwaway database; non-zero exit with a value-free, migration-naming/category message on an unreachable host, bad credentials and an invalid connection string (assert the password and connection string never appear in output); starting the web host against an empty database does **not** create schema (FR-011, FR-012, FR-012a)
- [X] T028 [P] [US2] Perform the migration safety review and record it in `specs/012-hardening-release/release-validation.md`: a per-migration table of every `Up` operation, confirming no table/column drops (only the one replaced `DropIndex` in `AddRolesAndPermissions`), reviewing the data SQL of `SeedAdministrativePermissions` and `MoveAuditPermissionToAuthNamespace`, and classifying anything destructive as intentional+documented or fixing it with a corrective migration (FR-010, research R16)
- [X] T029 [US2] Create `scripts/generate-migration-script.sh` (run in the SDK container: `dotnet ef migrations script --idempotent` over the API/Infrastructure projects to `artifacts/gaussauth-schema.sql`; no credentials; `set -euo pipefail`; fails clearly if the tool is missing) and add `artifacts/` to `.gitignore`
- [X] T030 [US2] Verify the concurrent-migrator assumption: run two `migrate` commands at once against one throwaway database and record the observed behavior in `specs/012-hardening-release/release-validation.md`; if EF Core locking does not serialize them, change the migration-command contract and `docs/database.md` wording to require a single migrator instead of claiming safety — **outcome: the assumption was false; fixed in `DatabaseMigrator` with an advisory lock instead of weakening the contract (tests in `concurrentMigrationTests.test.cs`)**

**Checkpoint**: US2 independently verifiable — migrate-from-zero, upgrade-with-data and failure diagnostics proven; SQL script generator exists (equivalence is verified against the image in US5).

---

## Phase 5: User Story 3 — Health and Readiness Reporting (Priority: P1)

**Goal**: unauthenticated, minimal, empty-bodied liveness and readiness signals that report dependency loss and recovery without leaking anything.

**Independent Test**: readiness 204 with healthy dependencies; stop the database ⇒ liveness 204, readiness 503; restore ⇒ 204 within 10 s; unusable storage or pending migrations ⇒ 503; no body or detail in any response.

- [X] T031 [P] [US3] Create the database readiness check in `src/GaussAuth.Infrastructure/Health/databaseReadiness.check.cs` (`IHealthCheck`): connection can be opened within a short timeout **and** no pending migrations; unhealthy for unreachable or behind-schema; logs the cause server-side only on state transitions, never returning it
- [X] T032 [P] [US3] Create the profile-storage readiness check in `src/GaussAuth.Infrastructure/Health/profileStorageReadiness.check.cs` (`IHealthCheck`): root exists and a create/delete probe succeeds within a short timeout; unhealthy otherwise; cause logged once per transition only
- [X] T033 [US3] Add `/health/ready` in `src/GaussAuth.Api/DependencyInjection/readinessEndpoints.extension.cs`: `AddHealthChecks` with the two checks, `MapHealthChecks` with an empty-body response writer and status mapping Healthy ⇒ 204 / Unhealthy ⇒ 503, evaluation cache ≤ 5 s, no named rate-limit policy; keep `/health/live` as the existing dependency-free 204; wire both into `src/GaussAuth.Api/Program.cs` (FR-013, FR-026–029, FR-028a, `contracts/health-endpoints.md`)
- [X] T034 [P] [US3] Write `readinessTests` in `tests/GaussAuth.Foundation.Tests/readinessTests.test.cs`: ready with healthy dependencies; database unreachable (invalid port) ⇒ 503 while `/health/live` stays 204; throwaway database with no migrations applied ⇒ 503; unusable storage root ⇒ 503; recovery to 204 within 10 s after the dependency returns and a 503 within 10 s of the outage (SC-006, FR-013); all responses have empty bodies and no dependency/version/config headers; server log contains the cause once per transition; liveness never touches the database

**Checkpoint**: US3 independently verifiable.

---

## Phase 6: User Story 4 — Verified Release Build and Full Regression (Priority: P1)

**Goal**: a clean Release build and the essential regression suite pass, a recorded consistency review covers all eleven prior features, and any release-blocking defect found is fixed with a test.

**Independent Test**: from a clean checkout restore → Release build → full suite against a clean database; all green; every review bullet has recorded evidence.

- [X] T035 [US4] Review every Release build warning and classify by category (correctness, security, nullability, resource lifetime, API misuse vs. style): fix the first group, justify the rest, and record each in the *Build* section (FR-001, FR-002) of `specs/012-hardening-release/release-validation.md` (FR-002)
- [X] T036 [US4] Build the consistency-review evidence table in `specs/012-hardening-release/release-validation.md`: for every bullet under *Full feature consistency review* in `spec.md` (users, applications/memberships, roles/permissions, login, sessions, password management, authorization contract, security/audit, profile files, admin operations) cite the existing test that proves it, or mark a gap; record gaps in the Findings table with a release-blocking yes/no (FR-004)
- [X] T037 [US4] For each **release-blocking** gap from the review, add one focused regression test in the matching existing suite under `tests/GaussAuth.Foundation.Tests/` (e.g. `applicationsTests.test.cs`, `sessionsAccessTests.test.cs`, `administrationTests.test.cs`) and fix any underlying defect; add no tests for non-blocking items and no coverage-only tests (FR-005, FR-003)
- [X] T038 [P] [US4] Write `cryptographicConfigurationTests` in `tests/GaussAuth.Foundation.Tests/cryptographicConfigurationTests.test.cs`: a credential issued with the configured issuer/lifetimes validates and a mismatched issuer/key fails; Production-class names (including `Staging`) refuse an absent signing key while `Development` does not; the signing key is ECDSA P-256 and the published key set matches it; no development key is accepted outside `Development` (FR-017, FR-020, FR-025)
- [X] T039 [P] [US4] Extend `tests/GaussAuth.Foundation.Tests/consumerCredentialTests.test.cs` only where missing to prove FR-024: independent per-Application credentials, hash-only storage, current/previous rotation and retirement, plaintext only in issue/rotate responses, and no secret in logs, audit events or error bodies (use `tests/GaussAuth.Foundation.Tests/capturingLogger.provider.cs`)
- [X] T040 [US4] Run the full Release suite against a clean PostgreSQL 17 and record counts and any failures in the *Tests* section of `specs/012-hardening-release/release-validation.md`; every failure is fixed or recorded as a finding before this story is complete (depends on all earlier tasks in this phase)

**Checkpoint**: US4 evidence recorded; no open release-blocking regression.

---

## Phase 7: User Story 5 — Deployable Runtime (Priority: P2)

**Goal**: a reproducible, non-root production image and an evaluation composition with persistent storage, correct migrate-then-start ordering and clean shutdown.

**Independent Test**: build the image, run compose against a clean PostgreSQL 17, upload an avatar, recreate the API container, and confirm the avatar and a reset credential survive and readiness is 204.

- [X] T041 [US5] Create the multi-stage `Dockerfile` at the repository root per `contracts/runtime-package.md` (FR-040, FR-012a, FR-012b, FR-012d): `sdk:10.0` stage restores, publishes in Release, and generates `/app/db/gaussauth-schema.sql` with `dotnet ef migrations script --idempotent`; `aspnet:10.0` runtime stage runs as the built-in non-root `app` user, creates `/data/profile-images` and `/data/keys` owned by `app` and declared as volumes, sets `ASPNETCORE_ENVIRONMENT=Production` and `ASPNETCORE_HTTP_PORTS=8080`, **does not** set `ASPNETCORE_FORWARDEDHEADERS_ENABLED`, and uses exec-form `ENTRYPOINT ["dotnet","GaussAuth.Api.dll"]`; no SDK, source, tests, keys or `.env` in the final stage
- [X] T042 [P] [US5] Update `.dockerignore` to exclude `specs/`, `tests/`, `docs/`, `.git/`, every `.env*` file, `artifacts/`, `bin/`, `obj/`, `.profile-images/`, and any `*.pem`
- [X] T043 [P] [US5] Create `compose.release.yml` at the repository root per `contracts/runtime-package.md`: `postgres:17` with named volume and `pg_isready` healthcheck; one-shot `migrate` service (`command: ["migrate"]`, depends on healthy postgres); `api` with `depends_on: migrate: condition: service_completed_successfully`, port 8080 published to localhost only, volumes for `/data/profile-images` and `/data/keys`, signing key as a mounted file via `Sessions__Signing__PrivateKeyPemFile`, `ProfileImages__RootPath`, `ProfileImages__StorageIsPersistent=true`, `DataProtection__KeysPath=/data/keys`, `stop_grace_period` above the host shutdown timeout; leave `compose.dev.yml` and the AI development container untouched (FR-042, FR-012c)
- [X] T044 [P] [US5] Create `.env.release.example` (placeholders only, `replace_with_*` style, mirroring `.env.example`) and add `.env.release` and `*.pem` to `.gitignore`
- [X] T045 [US5] Set an explicit `HostOptions.ShutdownTimeout` (30 s) in `src/GaussAuth.Api/Program.cs` and review shutdown safety: confirm `LocalProfileImageStorage` stages then moves files and cleans up on cancellation, and that audited administrative changes commit atomically; write `tests/GaussAuth.Foundation.Tests/gracefulShutdownTests.test.cs` proving a cancelled upload leaves no partial file under the storage root and no orphan metadata, and that a read-only or full storage root yields a stable error response with no partial file (spec edge case); fix and record any defect found (FR-039, research R13)
- [X] T046 [P] [US5] Write `dataProtectionPersistenceTests` in `tests/GaussAuth.Foundation.Tests/dataProtectionPersistenceTests.test.cs`: two hosts sharing one `DataProtection:KeysPath` — a password-reset credential generated by host A validates in host B (simulating restart/replica); with different key paths it does not (documents why persistence is required)
- [X] T047 [US5] Build the image (`docker build -t gaussauth:release .`) and verify and record in `specs/012-hardening-release/release-validation.md`: configured user is non-root, `dotnet --list-sdks` inside the image reports none, `docker history` shows no secrets, SkiaSharp loads (an avatar upload succeeds in the container; if it fails, change the base image and update `contracts/runtime-package.md`)
- [X] T048 [US5] Verify SQL-script equivalence per quickstart scenario 3: extract `/app/db/gaussauth-schema.sql` with `docker create`/`docker cp`, apply it to fresh database B and run `migrate` against fresh database A, then compare `pg_dump --schema-only` and `__EFMigrationsHistory`; confirm the script contains no credentials and the image embeds none (FR-012b, FR-012d); record the result
- [X] T049 [US5] Run the compose stack and verify and record: migrate-then-start ordering; a deliberately broken connection string makes `migrate` exit non-zero and `api` never starts (FR-012c); readiness 204; avatar still retrievable after `up -d --force-recreate api` (SC-010); `docker stop` exits 0 within the grace period with no partial files; the no-trusted-proxy warning appears (quickstart scenarios 4, 5, 9)

**Checkpoint**: US5 independently verifiable.

---

## Phase 8: User Story 6 — Secure-by-Default HTTP and Proxy Behavior (Priority: P2)

**Goal**: forwarded headers trusted only from explicit proxies; HSTS/HTTPS behavior deliberate; no server leakage; CORS denied; limits enforced; every endpoint rate-limited or explicitly exempt; safe error contract with server-side diagnostics.

**Independent Test**: spoof forwarding headers from an untrusted source, exercise error/limit paths in a Production-class host, and enumerate endpoints for rate-limit coverage.

- [X] T050 [P] [US6] Create forwarded-headers setup in `src/GaussAuth.Api/DependencyInjection/forwardedHeadersSetup.extension.cs` (validator in `forwardedHeadersOptions.validator.cs`, options in `forwardedHeadersOptions.options.cs`): read `ForwardedHeaders:KnownProxies` and `ForwardedHeaders:KnownNetworks`; when both are empty do **not** add the middleware (direct-deployment behavior) and log the structured startup warning itself (forwarded headers ignored; behind a real proxy the remote address, HTTPS detection and dependent policies may not represent the real client); otherwise `XForwardedFor | XForwardedProto` only, `ForwardLimit = 1`, default known lists cleared then replaced; validate with `IValidateOptions` — unparsable entries and any trust-anyone network (`0.0.0.0/0`, `::/0`) fail startup naming the key; refuse startup when `ASPNETCORE_FORWARDEDHEADERS_ENABLED` is truthy while no explicit trust list exists (FR-031, FR-031a, FR-031b, research R3)
- [X] T051 [US6] Reorder and harden the pipeline in `src/GaussAuth.Api/Program.cs`: forwarded headers first (before HSTS, HTTPS redirection and `UseRateLimiter`); `UseHsts` only for Production-class (never `Testing`); `UseHttpsRedirection` only when `HttpsRedirection:HttpsPort` is configured; `AddServerHeader = false` on Kestrel; no CORS added (FR-032, FR-033, research R6)
- [X] T052 [P] [US6] Update `src/GaussAuth.Api/DependencyInjection/safeExceptionHandler.handler.cs` to log the exception object server-side (structured, correlated with the existing trace id) while the response stays the generic problem document; confirm no `EnableSensitiveDataLogging` anywhere (FR-030, FR-036–038, research R8)
- [X] T053 [P] [US6] Write `forwardedHeadersTests` in `tests/GaussAuth.Foundation.Tests/forwardedHeadersTests.test.cs`: spoofed `X-Forwarded-For`/`-Proto` from an untrusted source changes neither client address nor scheme nor the rate-limit bucket; from a configured trusted proxy they are honored and the limiter partitions by the forwarded client; no lists ⇒ ignored plus warning; invalid entry, `0.0.0.0/0`, `::/0` and the env-var trap are refused at startup (SC-012)
- [X] T054 [P] [US6] Write `httpBaselineTests` in `tests/GaussAuth.Foundation.Tests/httpBaselineTests.test.cs`: forced 500 (using `tests/GaussAuth.Foundation.Tests/testFailureStartupFilter.filter.cs`) returns the stable generic problem document with no stack trace, path, SQL, ORM/Identity names or raw exception and the exception is logged server-side; `X-Content-Type-Options: nosniff` present; no `Server` header; HSTS only on HTTPS requests in Production-class and never in `Testing`; simple and preflight cross-origin requests yield no `Access-Control-*` headers; oversized body ⇒ 413; over-limit avatar upload rejected at the configured limit and request/upload limits are explicitly configured (FR-034); error content types are `application/problem+json`/`text/plain` as designed
- [X] T055 [P] [US6] Write `rateLimitCoverageTests` in `tests/GaussAuth.Foundation.Tests/rateLimitCoverageTests.test.cs`: enumerate `EndpointDataSource` and assert every endpoint except the explicit exempt list (`/health/live`, `/health/ready`, each with a stated rationale) carries a named rate-limit policy (directly or via the `/admin` group), that the sensitive groups from FR-035 (login, recovery, reset, authorization-context, upload, administration) map to distinct policies, and that exceeding one group's limit does not affect another
- [X] T056 [US6] Review the per-group rate-limit defaults against their abuse profile and record the justification table in `specs/012-hardening-release/release-validation.md`; change a default only if the review finds it unsafe (record any change in the Findings table and `contracts/configuration-reference.md`)

**Checkpoint**: US6 independently verifiable.

---

## Phase 9: User Story 7 — Operational Documentation, Secret Hygiene and First-Administrator Bootstrap (Priority: P2)

**Goal**: an operator unfamiliar with the project can bootstrap, deploy, configure, upgrade, back up and restore it from the docs alone; no real secret is in the repository; an empty system can be taken to a working global administrator without touching the database.

**Independent Test**: follow `docs/` on a clean machine from empty database to a global administrator performing a global operation; run the secret scan with zero findings.

- [X] T057 [P] [US7] Create `FirstAdministratorBootstrap` in `src/GaussAuth.Infrastructure/Bootstrap/firstAdministratorBootstrap.service.cs` (outcome type in `firstAdministratorBootstrap.result.cs`): reuses `CreateUserHandler` (`src/GaussAuth.Application/Users/CreateUser/createUser.handler.cs`) so email normalization/uniqueness and the Identity password policy apply; refuses (no write) when any user already exists; returns a value-free result category (`users-exist`, `invalid-email`, `password-policy`, `missing-input`, `database`, `configuration`) and the new UserId; never logs or returns the password (FR-044, `contracts/bootstrap-command.md`)
- [X] T058 [US7] Implement the `bootstrap-admin` command host in `src/GaussAuth.Api/Commands/bootstrapAdminCommand.service.cs`: minimal host (database context, Identity, create-user use case; **no HTTP**); read `Bootstrap:AdministratorEmail`, `Bootstrap:AdministratorPassword` or `Bootstrap:AdministratorPasswordFile`, optional first/last name (defaults `Auth`/`Administrator`) from configuration/environment only — never command-line arguments; on success print the UserId and the exact `Administration__GlobalAdministratorUserIds__0=<UserId>` instruction and exit 0, otherwise exit ≠ 0 with the category; register in `src/GaussAuth.Api/Commands/commandLine.service.cs`
- [X] T059 [P] [US7] Write `bootstrapAdministratorTests` in `tests/GaussAuth.Foundation.Tests/bootstrapAdministratorTests.test.cs`: on a throwaway database with migrations applied the command creates exactly one active user and prints its id; the password never appears in stdout/stderr/logs/security events; a second run exits ≠ 0 with `users-exist` and writes nothing, including when the administrator list is empty; weak password and invalid email are rejected value-free; the created user has **no** global authority until its id is configured (global operation rejected, then accepted after the policy lists it); the web host exposes no bootstrap route (FR-044, FR-045, SC-013)
- [X] T060 [P] [US7] Create `scripts/scan-secrets.sh` (tracked files and git history): flag PEM private-key headers, connection strings with non-placeholder passwords, and long high-entropy assignments in config/compose/env-example files; allow-list `replace_with_*` and `not_a_secret`; non-zero exit on any finding; `set -euo pipefail`
- [X] T061 [P] [US7] Write `repositorySecretHygieneTests` in `tests/GaussAuth.Foundation.Tests/repositorySecretHygieneTests.test.cs`: scan tracked source, config, compose and env-example files for private-key PEM headers and non-placeholder password-bearing connection strings; fail with the file path only (never the matched text) (FR-022)
- [X] T062 [US7] Run `scripts/scan-secrets.sh` over the working tree and full git history, fix or justify each finding, and record the result in the *Security* section of `specs/012-hardening-release/release-validation.md` (SC-011)
- [X] T063 [P] [US7] Write `docs/deployment.md` (FR-043, FR-028a): image and compose usage, rollout order (migrate → start → readiness), health probes and the guidance that health endpoints must not be published unnecessarily and are controlled by network/reverse proxy/firewall/ingress, HTTPS expectations (TLS terminates upstream; HSTS effective via trusted proxy), required reverse-proxy forwarding for scheme and client IP and the trusted-proxy configuration with the warning behavior and the `ASPNETCORE_FORWARDEDHEADERS_ENABLED` trap, logging and correlation (`X-Correlation-Id`/trace id), graceful shutdown
- [X] T064 [P] [US7] Write `docs/configuration.md` (FR-043) from `contracts/configuration-reference.md`: complete key reference (meaning, default, Production requirement), the five Production-required keys, environments and the Production-class rule, secret injection (environment, mounted files, platform secrets; no vendor mandated), precedence, keys that must not be set, rate-limit table, and the signing-key terminology — **ECDSA on the NIST P-256 curve (ES256)**, asymmetric, kept distinct from the symmetric per-Application consumer secrets — plus consumer-secret rotation procedure with the retirement window (FR-023, FR-024, FR-046)
- [X] T065 [P] [US7] Write `docs/database.md` (FR-043): migration mechanisms (one-off `migrate` command as the standard path, release SQL script as the reviewed-alternative, never at startup, single migrator at a time per the behavior verified in T030), upgrade procedure and rollback-by-restore, migration safety/data-effect notes, backup and restore of database **and** profile storage together with consistency guidance and what happens on mismatch, profile-storage root/permissions/persistence/volume behavior and behavior when storage is unavailable, and the Data Protection key ring: persistence is required to preserve Identity password-reset credentials and any other Data Protection–protected state across restarts and replicas, key files are key material (owner-only permissions, secret-grade, unencrypted at rest by default)
- [X] T066 [P] [US7] Write `docs/bootstrap.md`: the first global administrator procedure from `contracts/bootstrap-command.md` (migrate → `bootstrap-admin` with env/mounted-secret inputs → record UserId → set `Administration__GlobalAdministratorUserIds__0` → restart → verify a global operation → remove the bootstrap password), the explicit statement that an **empty list means no global authority** (feature 011 decision), and that the command refuses once any user exists
- [X] T067 [P] [US7] Write `docs/security-baseline.md`: HTTP baseline (headers, error contract, request/upload limits), CORS posture (denied by default; enabling browser access requires a future feature with an explicit allowlist), rate-limit groups and rationale, consumer-secret model, cryptographic configuration (ECDSA P-256 signing vs. symmetric consumer secrets), what is never logged
- [X] T068 [P] [US7] Write `docs/limitations.md`: password-recovery delivery requires an operator-supplied adapter in Production (no SMTP/webhook shipped), single active signing key (rotation needs a coordinated redeploy), key ring unencrypted at rest, no encrypted-at-rest options, no built-in HA/load guarantees, no detailed diagnostics endpoint; confirm each item is non-release-blocking per the spec definition
- [X] T069 [US7] Walk the documentation on a clean checkout and record in `release-validation.md`: empty database → `migrate` → `bootstrap-admin` → configure administrator → restart → global operation succeeds; then upgrade from the previous image to the new one and back up/restore database plus profile storage; fix any doc step that does not work as written (quickstart scenarios 6 and 12, SC-009, SC-013)

**Checkpoint**: US7 independently verifiable.

---

## Phase 10: User Story 8 — Observable Operation Without Sensitive Leakage (Priority: P3)

**Goal**: operators can trace failures through logs and correlation ids with Production-safe defaults, and no sensitive value ever appears.

**Independent Test**: trigger configuration, database, security-sensitive and unexpected-failure scenarios; confirm meaningful entries exist and no token/secret/password/reset credential/file content/Authorization header appears.

- [X] T070 [P] [US8] Create logging configuration files with no secrets: `src/GaussAuth.Api/appsettings.json` (Default Information; `Microsoft.AspNetCore` and `Microsoft.EntityFrameworkCore` Warning; `Microsoft.Hosting.Lifetime` Information), `src/GaussAuth.Api/appsettings.Production.json` (built-in JSON console formatter including scopes so `TraceId` is queryable) and `src/GaussAuth.Api/appsettings.Development.json` (more verbose); confirm they are copied on publish and that no required-in-Production key has a default there
- [X] T071 [P] [US8] Write `sensitiveLoggingTests` in `tests/GaussAuth.Foundation.Tests/sensitiveLoggingTests.test.cs`: run the sensitive scenarios (login success/failure, lockout, password change/recovery/reset, session validate/renew/logout, consumer authorization, administrative secret issue/rotate, avatar upload, forced 500, startup with secret-bearing settings) with a capturing logger and assert none of access tokens, consumer secrets, passwords, reset credentials, complete `Authorization` headers, file content, connection strings or key material appears in any log, security event or error body (FR-037, SC-007)
- [X] T072 [P] [US8] Write `correlationTests` in `tests/GaussAuth.Foundation.Tests/correlationTests.test.cs`: an unexpected-failure log entry, the response `X-Correlation-Id`, and a persisted SecurityEvent for the same request share the existing trace id; no new identifier scheme is introduced (FR-038)
- [X] T073 [US8] Verify and record operational log entries exist (start, shutdown, critical configuration error, database connectivity failure on readiness, migration failure from the `migrate` command, unexpected application failure) by capturing real output from the image/compose run in `specs/012-hardening-release/release-validation.md`; add the missing entry where one is absent (FR-021)

**Checkpoint**: US8 independently verifiable.

---

## Phase 11: Polish & Cross-Cutting Concerns

- [X] T074 Update `.env.example` and `compose.dev.yml` comments/variables for the new settings (`DataProtection__KeysPath` optional in Development, `ProfileImages__StorageIsPersistent`, `Bootstrap__*`, `ForwardedHeaders__*`) without changing development behavior or committing any value; keep the development container workflow unchanged
- [X] T075 Create `docs/README.md` indexing the six operator documents (deployment, configuration, database, bootstrap, security-baseline, limitations) with a one-line purpose each so the set is discoverable
- [X] T076 Re-run every quickstart scenario (`quickstart.md` 1–12) end to end after all stories, fix or record deviations, and mark each in `release-validation.md`
- [X] T077 Final gate: from a clean checkout `dotnet restore`, `dotnet build -c Release`, `dotnet test -c Release` against a clean PostgreSQL 17 (FR-001, FR-003); review the whole branch diff and confirm no new NuGet package, no HTTP endpoint other than `/health/ready`, no SMTP/webhook adapter and no CORS allowlist were added (FR-006); update `release-validation.md` with final results and confirm SC-001…SC-014 each have recorded evidence
- [X] T078 Close out the Findings table: every release-blocking finding is *fixed with a test* (none documented-only); every remaining item is a non-blocking limitation listed in `docs/limitations.md`; the sign-off section lists zero open release-blocking items (SC-008)

---

## Dependencies & Execution Order

### Phase dependencies

- **Setup (Phase 1)**: no dependencies.
- **Foundational (Phase 2)**: depends on Setup; **blocks all user stories**.
- **US1 (P1)**, **US2 (P1)**, **US3 (P1)**, **US4 (P1)**: each depends only on Foundational, except as noted below. US4's evidence tasks (T036, T040) run after US1–US3 land so the recorded results reflect the new behavior.
- **US5 (P2)** depends on US1 (required keys, recovery behavior), US2 (`migrate` command, SQL generator) and US3 (readiness) because the image and compose run exercise all three.
- **US6 (P2)** depends on US1 (rate-limit options) and edits `Program.cs` after US3's wiring.
- **US7 (P2)** depends on Foundational (command dispatcher) and US2 (migrator pattern, throwaway DB fixture); the documentation tasks should follow US1, US2, US5, US6 so they describe final behavior.
- **US8 (P3)** depends on US1, US3, US5 for the real log output it verifies.
- **Polish** depends on all stories.

### Cross-task dependencies inside stories

- US1: DataProtection/ProfileImages options → DI wiring task; rate-limit options → `apiServiceCollectionExtensions` replacement; `productionStartupValidationTests` is written first and is expected to fail until the implementation tasks land.
- US2: `DatabaseMigrator` → `migrate` command → `migrateCommandTests`; concurrent-migrator verification after the command exists; the migration safety review may run in parallel with everything.
- US3: both readiness checks → endpoint wiring → `readinessTests`.
- US5: Dockerfile → image verification → SQL-equivalence check → compose run.
- US7: `FirstAdministratorBootstrap` → `bootstrap-admin` command → `bootstrapAdministratorTests`.

### `Program.cs` edit order (not parallelizable)

Foundational dispatcher → Foundational failure handling → US1 diagnostics/request-limit key → US3 health wiring → US5 shutdown timeout → US6 pipeline reorder.

## Parallel Opportunities

- Foundational: exception, Production-class extension, throwaway-database fixture and process-launcher fixture touch different files.
- US1: signing key (`accessCredentialSigningKey.service.cs`), administrator/retention files, profile-images options, DataProtection options, rate-limit options and the new tests can proceed together; the two DI/`Program.cs` edits follow.
- US2: `DatabaseMigrator`, from-zero tests, upgrade test, migrate-command tests and the safety review are separate files.
- US3: the two readiness checks in parallel.
- US6: forwarded-headers setup, exception handler logging and the three test files in parallel.
- US7: bootstrap service, secret scan script, hygiene test and all six documents in parallel.
- US8: appsettings files and the two test files in parallel.

### Example: User Story 1 parallel start

```text
T010 productionStartupValidationTests.test.cs      (expected red at first)
T011 accessCredentialSigningKey.service.cs
T013 configuredGlobalAdministratorPolicy / securityAuditRetentionPolicy
T014 profileImagesOptions.options.cs
T015 dataProtectionOptions.options.cs
T017 rateLimitOptions.options.cs
T020 recoveryWithoutDeliveryTests.test.cs
```

## Implementation Strategy

### MVP (smallest releasable slice)

1. Phase 1 → Phase 2 (foundation).
2. **US1** (safe startup, recovery without channel) — the highest-risk gap.
3. **US2** (migrations from zero and upgrade, `migrate` command) and the **US7 bootstrap command + tests** — without these an operator cannot create or operate a system at all; the bootstrap gap is release-blocking and must not slip behind documentation work.
4. **US3** (readiness).
5. **STOP and validate**: run the Release suite; this is the minimum that makes the platform operable.

### Incremental delivery

Then US4 (formal regression evidence) → US6 (edge hardening) → US5 (image/compose, which verifies everything together) → remaining US7 documentation and secret scan → US8 → Polish. Each phase ends with its checkpoint; do not start the next dependent phase with a red suite.

### Rules for implementers

- Release-blocking findings are fixed with a regression test, never documented away (FR-005).
- No new NuGet packages; no new HTTP endpoints other than `/health/ready`; no SMTP/webhook adapter; no CORS allowlist.
- Do not infer persistence from paths; do not print or log secrets, keys, passwords or connection strings anywhere (including startup errors).
- Record every verification outcome in `specs/012-hardening-release/release-validation.md` as you go.
