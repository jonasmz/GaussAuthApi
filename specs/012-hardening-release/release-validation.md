# Release Validation Record: 012-hardening-release

Evidence log for the release-hardening feature. Sections follow `data-model.md` → *Release Validation Record*. Updated as each task completes.

## Build

### Baseline (before any 012 change) — T001

| Item | Result |
|------|--------|
| Date | 2026-10-02 |
| Branch / base | `012-hardening-release` on top of `main` at `d867c72` (feature 011 merged) |
| SDK | .NET SDK 10.0.401 (`mcr.microsoft.com/dotnet/sdk:10.0` dev container) |
| `dotnet restore GaussAuth.slnx` | success, all projects up to date |
| `dotnet build GaussAuth.slnx -c Release` | success, 0 errors, **1 warning** |
| `dotnet test GaussAuth.slnx -c Release` | **189 passed, 0 failed, 0 skipped** (1 m 16 s) against the dev PostgreSQL 17 |

Warnings at baseline (reviewed under T035):

| Warning | Location | Category | Disposition |
|---------|----------|----------|-------------|
| CS8619 nullability of `string?[]` vs `string[]` | `tests/GaussAuth.Foundation.Tests/profileImageTests.test.cs:768` | nullability, **test code only** | to be reviewed in T035 (no production impact) |

### Tooling — T002

| Item | Result |
|------|--------|
| `dotnet tool restore` | `dotnet-ef` 10.0.12 restored from `.config/dotnet-tools.json` |
| `dotnet ef --version` | 10.0.12 |

### Final warning review — T035

| Warning | Category | Disposition |
|---------|----------|-------------|
| CS8619 in `profileImageTests.test.cs` (`Path.GetFileName` nullability) | Nullability, test code | **Fixed**: filter valid names and explicitly narrow them to non-null before materializing the array. Final Release build: 0 warnings, 0 errors. |

## Tests

Baseline above. Final-run results are recorded here by T040 and T077.

### Phase 5 readiness — T031–T034

`ReadinessTests` (4 cases) passed in Release. `/health/ready` evaluates database connectivity/current migrations and a profile-storage create/delete probe with two-second check timeouts; its result is cached for five seconds. It returns 204 or 503 with `Content-Length: 0`; `/health/live` remains a dependency-free 204. The tests cover healthy dependencies, an invalid database port, no migrations on a throwaway database followed by recovery to 204 after migration, and a filesystem path that is a file rather than a usable directory.

### Final Release regression — T040

| Item | Result |
|------|--------|
| Environment | clean `public` schema in the development PostgreSQL 17 container |
| `dotnet build GaussAuth.slnx -c Release` | success, **0 warnings, 0 errors** |
| `dotnet test GaussAuth.slnx -c Release` | **263 passed, 0 failed, 0 skipped**; TRX `phase6-final.trx` |
| Failures found during the first clean run | 2; both fixed before final run: an over-broad response assertion treated ordinary email text containing “recovery” as secret material, and the membership repository missed the actual PostgreSQL alternate-key constraint name when translating a concurrent duplicate to a normal duplicate result. |

### Full feature consistency review — T036–T039

| Review area | Evidence | Gap / disposition |
|-------------|----------|-------------------|
| Users | `CreateUserTests`, `UserRetrievalTests`, `UserActivationTests` | Covered |
| Applications and memberships | `ApplicationsTests`, `ApplicationMembershipsTests`, `AdministrationTests.Concurrent_identical_assignments_and_memberships_leave_exactly_one_record` | Concurrent duplicate membership was release-blocking; fixed by recognizing `AK_ApplicationMemberships_UserId_ApplicationId`. |
| Roles and permissions | `RolesPermissionsTests`, `EffectivePermissionsTests` | Covered, including application isolation |
| Login | `AuthenticationLoginTests` | Covered |
| Sessions | `SessionsAccessTests`, `SessionDomainTests` | Covered, including expiry and revocation |
| Password management | `PasswordManagementTests`, `RecoveryWithoutDeliveryTests` | Covered |
| Authorization contract | `AuthorizationContractTests` | Covered |
| Security and audit | `SecurityAuditTests`, `AdministrationTests.Every_administrative_change_records_one_event_with_actor_and_target` | Covered |
| Profile files | `ProfileImageTests`, `ProfileUpdateTests` | Covered |
| Administrative operations | `AdministrationTests` | Covered |
| Cryptographic configuration | `CryptographicConfigurationTests` (6 cases) | Covered: configured issuer/key validation, mismatches, P-256/JWK publication and Development-only ephemeral keys. |
| Consumer credentials | `AdministrationTests.Consumer_secret_lifecycle_shows_the_value_once_and_keeps_the_previous_until_retired`, `ConsumerCredentialTests` | Covered: isolation, hash-only persistence, rotation, retirement and secret-free logs/audit/errors; no additional test was missing. |

### After Phase 1–2 (foundation) — 2026-10-02

| Item | Result |
|------|--------|
| Release build | success, 0 errors, 1 warning (unchanged CS8619, test code only) |
| Release test run | **194 passed, 0 failed** (189 baseline + `Unknown_subcommand…`, 3 reporter tests, 1 throwaway-database test) in 1 m 15 s |
| Behavior checks | `dotnet GaussAuth.Api.dll migrate` and `bootstrap-admin` exit 70 ("not available in this build") until US2/US7 register handlers; a mistyped subcommand exits 64 and never starts the web host; no-argument start and `WebApplicationFactory` flow unchanged (all pre-existing tests still pass) |

### After Phase 3 (US1, safe startup) — 2026-10-02

| Item | Result |
|------|--------|
| Release test run | **235 passed, 0 failed** (194 after phase 2 + 31 startup-validation cases, 3 recovery tests, 2 profile-options tests and adjusted existing tests) in 2 m 05 s |
| Manual check | Production without `DataProtection:KeysPath` exits 1 with one JSON Critical line naming only the key; Production valid start logs the summary, `Application started`, and `Shutdown started/completed` on SIGTERM |
| Existing tests changed | `startupTests` (key-naming messages, new required Production keys), `profileImageTests` (persistence declaration cases added), `administrationTests` (now asserts `StartupConfigurationException` with the setting key and no echoed value) — no assertion weakened |

## Migrations

Environment: PostgreSQL 17 (dev compose), every check on **uniquely named empty databases** created and dropped by `ThrowawayDatabase` (never the evolved dev database).

### Results (T023–T030) — 2026-10-02

| Check | Evidence | Result |
|-------|----------|--------|
| From zero: 10 migrations apply in the documented order; re-run is a no-op; compiled chain equals the release chain | `ReleaseMigrationValidationTests.Migrations_apply_in_order_from_an_empty_database_and_are_idempotent` | Pass |
| Critical constraints/indexes exist after migration from zero (unique normalized email on `Users` and `AspNetUsers`, unique application code, unique membership per user+application, application-scoped unique role name / permission code, role-permission and user-role pair uniqueness, `(Id, ApplicationId)` alternate keys, one consumer credential per application, session and 6 security-event indexes, two composite 2-column FKs each on `RolePermissions` and `UserRoles`) | `…Critical_constraints_and_indexes_exist_after_migrating_from_zero` | Pass |
| The application starts and serves requests (create Application, list, seeded permissions) against the migrated schema | `…The_application_starts_and_serves_requests_against_the_migrated_schema` | Pass |
| Upgrade from the previous release state (`…AddSecurityEvents`) with seeded users, applications, memberships, two same-named roles in different Applications, legacy audit permissions with assignments, sessions and security events: no row lost, 18 administrative permissions seeded once, legacy audit permission deactivated and kept, assignments copied per Application with no cross-application leakage, upgraded structure equals a from-zero database | `ReleaseMigrationUpgradeTests.Upgrading_from_the_previous_release_with_data_…` | Pass |
| Seed migration fails closed on a reserved `auth.` permission: failing migration named, rolled back, history and data unchanged | `…A_reserved_permission_prefix_makes_the_seed_migration_fail_closed…` | Pass |
| `migrate` command as a real process: success, no-op, unreachable server (`connection`), rejected credentials (`authentication`), missing/invalid connection string (key only), failing migration (`migration-failed`, id + SQLSTATE `42P07`); none leaks the password, user, host, SQL or a stack trace | `MigrateCommandTests` (8 cases) | Pass |
| The running web host never applies migrations (empty database stays empty after startup in Production) | `MigrateCommandTests.The_web_host_never_applies_migrations_on_startup` | Pass |
| Idempotent SQL script (same generator as `dotnet ef migrations script --idempotent`) yields the same schema, history rows/product versions and seeded data as the command; idempotent; upgrades a partially migrated database; contains no connection settings | `ReleaseMigrationScriptTests` (3 cases) | Pass |
| The generated file works with `psql` as a DBA would use it: applied with `ON_ERROR_STOP=1` to a new database (10 history rows), applied again with no error | manual, `scripts/generate-migration-script.sh` + `psql` in the postgres container | Pass (after the BOM fix, F-009) |
| Concurrency: two simultaneous `migrate` runs | `ConcurrentMigrationTests` (3 rounds) + manual experiment | **Initially failed** (2 of 3 rounds: PostgreSQL `42704`, `2BP01`) → fixed with an advisory lock (F-008); now 3 of 3 rounds exit `0,0`; a held lock yields category `locked` and no schema change |

### Migration safety review (T028) — operations in every `Up`

Method: operations extracted mechanically from each migration's `Up`; data SQL read in full.

| # | Migration | `Up` operations | Destructive? | Notes |
|---|-----------|-----------------|--------------|-------|
| 1 | `20261001012324_InitialIdentityFoundation` | 4 `CreateTable`, 4 `CreateIndex` | No | creates Identity tables |
| 2 | `20261001024704_AddUsersAndProfiles` | 2 `CreateTable`, 1 `CreateIndex` | No | |
| 3 | `20261001043323_AddApplicationsAndMemberships` | 2 `CreateTable`, 3 `CreateIndex` | No | |
| 4 | `20261001061232_AddRolesAndPermissions` | 4 `CreateTable`, 8 `CreateIndex`, 1 `AddUniqueConstraint`, **1 `DropIndex`** | No (index only) | `IX_ApplicationMemberships_UserId_ApplicationId` is replaced by the alternate key `AK_ApplicationMemberships_UserId_ApplicationId` on the **same columns**, inside the same transactional migration: uniqueness is never absent and no data changes. Existing duplicates would make it fail and roll back, not lose data. |
| 5 | `20261001085523_AddSessions` | 1 `CreateTable`, 1 `CreateIndex` | No | |
| 6 | `20261001202342_AddSecurityEvents` | 1 `CreateTable`, 5 `CreateIndex` | No | audit table created |
| 7 | `20261001232459_AddSecurityEventActor` | 1 `AddColumn` (nullable), 1 `CreateIndex` | No | existing audit rows keep a NULL actor |
| 8 | `20261002000000_SeedAdministrativePermissions` | 2 data SQL | No deletions | (a) **fails closed** if any permission already uses the reserved `auth.` prefix (transaction rolls back, nothing changes — intentional, tested); (b) inserts 9 administrative permissions per Application with `ON CONFLICT DO NOTHING` |
| 9 | `20261002000617_AddApplicationConsumerCredentials` | 1 `CreateTable` | No | stores hashes only |
| 10 | `20261002010000_MoveAuditPermissionToAuthNamespace` | 3 data SQL | No deletions | adds `auth.security.audit.read`, **copies** role assignments from the legacy permission (active only), then **deactivates** (does not delete) the legacy permission — history kept; tested for per-Application isolation |

Conclusions: no `Up` drops or rewrites user, security, authorization-history or audit data; no operation changes `AspNetUsers`, security stamps or sessions, so no active identity is invalidated; each migration runs in its own transaction (a failure rolls back that migration — proven by the fail-closed test). The only data-affecting steps are migrations 8 and 10, both additive/deactivating and documented in `docs/database.md`. `Down` methods contain `DropTable`/`DropColumn` and are **development-only**: the production rollback path is restoring the pre-upgrade backup, never a down-migration (documented in the migration-command contract).

## Runtime

Filled by T045–T049 (image, compose, persistence, shutdown).

### Phase 7 runtime package

The production image builds successfully from the Dockerfile. Inspection confirms non-root `app` (UID 1654), direct `dotnet GaussAuth.Api.dll` entrypoint, exposed port 8080, a non-empty `/app/db/gaussauth-schema.sql`, writable data directories, and no SDK in the runtime stage. The release compose validates its migrate-before-api dependency graph and declares persistent database, profile-image, and key-ring volumes. The application host shutdown timeout is 30 seconds and the compose grace period is 40 seconds. A temporary compose run completed PostgreSQL → migrate (10 migrations) → API startup successfully; the API process ran as UID 1654.

## Security

Filled by T050–T056 and T060–T062 (forwarded headers, headers/CORS/error shape, rate-limit coverage, secret scan).

### Phase 8 HTTP/proxy baseline

Forwarded headers are processed only from explicitly trusted proxies/networks and only for one `X-Forwarded-For`/`X-Forwarded-Proto` hop; missing trust configuration ignores them and warns, while the unsafe framework shortcut fails closed. Kestrel suppresses the Server header, HSTS is Production-class-only, HTTPS redirection needs an explicit port, CORS remains absent, and unexpected exceptions are logged with the trace id while responses stay generic. Focused Release phase tests: 7 passed, 0 failed. Full Release suite: **270 passed, 0 failed, 0 skipped** (`phases7-8.trx`).

### Phase 9 bootstrap, documentation and secret hygiene — T057–T069

`BootstrapAdministratorTests` executes `migrate` and `bootstrap-admin` as separate processes against a uniquely named PostgreSQL database. It created exactly one active user, emitted the UserId/configuration instruction, persisted the safe `user.created` event, and refused a second execution with `users-exist`; weak passwords and invalid addresses were rejected without echoing inputs. The global-administrator policy remains empty until the printed id is explicitly configured. No HTTP bootstrap route exists because command dispatch exits before web-host construction.

The six operator documents were walked through using that process evidence: empty database → migration → bootstrap → explicit administrator configuration. Existing migration upgrade/SQL-script tests provide the previous-release upgrade and restore-path validation; `docs/database.md` records the required joint PostgreSQL/profile-storage backup and rollback-by-restore procedure. `scripts/scan-secrets.sh` completed with exit 0 on 2026-10-02 over tracked configuration and reachable configuration history. Its findings report locations only; no finding needed remediation.

### Phase 10 observability and sensitive values — T070–T073

Focused Release tests (8 passed, 0 failed) cover JSON Production console logging with scopes, value-free forced-500 output/logs, and a persisted `application.failure.unexpected` audit event. The test proves the response `X-Correlation-Id`, the safe operational log entry, and the audit row carry the same existing trace id. Captured process/host output confirms start and graceful-shutdown entries; `MigrateCommandTests` records value-free migration database/connection failures, and `ReadinessTests` records database connectivity failure as a 503. Startup configuration failures are covered by `ProductionStartupValidationTests`; all paths avoid connection strings, Authorization values, passwords, key material and request/file content.

### Final polish and gate — T074–T078

Development composition was rendered with `.env.example`; its new Data Protection, storage-persistence, bootstrap, and forwarded-header variables are empty/optional, so the existing Development behavior remains unchanged. `docs/README.md` indexes all six operator guides. The secret scan exits 0.

Quickstart scenarios 1–12 were re-run or matched to their end-to-end regression evidence: Release restore/build/test; zero/upgrade/SQL migrations; Production startup validation; readiness failure/recovery; bootstrap; recovery without delivery; edge/proxy baseline; image/compose/persistence/shutdown; secret scan; consumer-secret/log review; and documentation procedure. The focused Release scenario group passed **32/32**. The final SDK gate ran `dotnet restore`, `dotnet build -c Release` (**0 warnings, 0 errors**) and `dotnet test -c Release` with exit 0. The branch review found no package-reference change, no newly introduced HTTP route other than the specified readiness endpoint, no SMTP/webhook adapter, and no CORS allowlist.

| Success criterion | Recorded evidence |
|---|---|
| SC-001–SC-005 | Release build, Production startup and migration validation tests |
| SC-006–SC-008 | Readiness, persistence, graceful-shutdown and security/audit tests |
| SC-009–SC-011 | Quickstart/documentation walk-through and secret scan |
| SC-012–SC-014 | Bootstrap, sensitive-logging and correlation regression tests |

## Findings

Release-blocking categories (spec): authentication correctness; authorization isolation; session revocation; credential secrecy; consumer-secret isolation; password/reset secrecy; audit integrity; database migration consistency; startup reliability; production configuration safety. A release-blocking finding can only be closed as **fixed with a regression test**.

| Id | Area | Finding | Release-blocking? | Disposition |
|----|------|---------|-------------------|-------------|
| F-001 | Password recovery | In Production-class environments the only recovery delivery adapter throws, and `RequestRecoveryAsync` generates the reset credential before delivering it and then discards it (FR-018a requires no generation without a channel). | Yes — password/reset secrecy, production configuration safety | **Fixed** (T019): `IRecoveryDelivery.IsAvailable`; the service returns before any lookup or generation. Tests: `RecoveryWithoutDeliveryTests` (identical responses, spy count 0, no events/file/log of the address, positive control proves the spy counts) |
| F-002 | Startup | Configuration failures surface as unhandled-exception stack traces, messages do not name the setting, and only the administration rate limit is validated. | Yes — startup reliability, production configuration safety | **Fixed** (T009–T018, T021): `StartupConfigurationException` + `StartupFailureReporter` (key-only Critical log, exit 1, no stack trace); every rate-limit group, lifetimes, lockout, request limit, retention, administrator list and key settings validated. Tests: `ProductionStartupValidationTests` (31 process-level cases incl. `Staging`), `StartupFailureReporterTests` |
| F-003 | Health | No readiness endpoint; a database outage cannot be reported. | Yes — startup reliability | **Fixed** (T031–T034): cached built-in health checks return empty 503 for database/storage outage or pending migrations and recover within 10 s. Tests: `ReadinessTests`. |
| F-004 | Bootstrap | No supported way to create the first user: user creation requires a global administrator and the global list only holds ids of existing users. | Yes — startup reliability, production configuration safety | **Fixed** (T057–T059): value-free, no-HTTP `bootstrap-admin` only creates the first active user and requires explicit authority configuration afterward. Regression: `BootstrapAdministratorTests`. |
| F-005 | Data Protection | Key ring defaults to ephemeral container storage; password-reset credentials are lost on restart and not shared across replicas. | Yes — production configuration safety | **Fixed** (T015, T016, T046): durable key-ring configuration and cross-restart persistence are covered by `DataProtectionPersistenceTests`. |
| F-006 | Forwarded headers | No forwarded-header handling; behind a proxy every client shares one rate-limit bucket. | No — documented behavior with startup warning (direct deployment) | **Fixed** (T050, T051): explicit trusted-proxy forwarding with regression coverage; direct deployment remains a documented warning. |
| F-008 | Migrations | Two simultaneous `migrate` runs corrupted the outcome: 2 of 3 rounds failed with PostgreSQL `42704` / `2BP01` (the framework's per-call locking was assumed sufficient but is not). | Yes — database migration consistency | **Fixed** (T023/T030): the migrator holds a PostgreSQL advisory lock for the whole run; held lock ⇒ category `locked`, no schema change. Tests: `ConcurrentMigrationTests` (3 rounds both `0`, plus held-lock test) |
| F-009 | Migration script | `dotnet ef` writes the SQL script with a UTF-8 BOM that `psql` rejects on the first statement. | No — operational (DBA path) | **Fixed** (T029): `scripts/generate-migration-script.sh` strips it and verifies no connection settings; applied with real `psql` |
| F-007 | Test warning | CS8619 in `profileImageTests.test.cs:768` (test code only). | No | **Fixed** (T035): explicit post-filter nullability narrowing; final Release build has 0 warnings. |
| F-010 | Membership concurrency | A concurrent duplicate membership hit PostgreSQL alternate key `AK_ApplicationMemberships_UserId_ApplicationId`, which was not translated to the normal duplicate result. | Yes — authorization isolation | **Fixed** (T037): repository recognizes the actual unique constraint. Regression: `AdministrationTests.Concurrent_identical_assignments_and_memberships_leave_exactly_one_record`. |

## Sign-off

Open release-blocking items: **0**. Every release-blocking finding is fixed with a regression test. Remaining operational constraints are non-blocking and listed in `docs/limitations.md`.
