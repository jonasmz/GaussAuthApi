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

## Tests

Baseline above. Final-run results are recorded here by T040 and T077.

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

## Security

Filled by T050–T056 and T060–T062 (forwarded headers, headers/CORS/error shape, rate-limit coverage, secret scan).

## Findings

Release-blocking categories (spec): authentication correctness; authorization isolation; session revocation; credential secrecy; consumer-secret isolation; password/reset secrecy; audit integrity; database migration consistency; startup reliability; production configuration safety. A release-blocking finding can only be closed as **fixed with a regression test**.

| Id | Area | Finding | Release-blocking? | Disposition |
|----|------|---------|-------------------|-------------|
| F-001 | Password recovery | In Production-class environments the only recovery delivery adapter throws, and `RequestRecoveryAsync` generates the reset credential before delivering it and then discards it (FR-018a requires no generation without a channel). | Yes — password/reset secrecy, production configuration safety | **Fixed** (T019): `IRecoveryDelivery.IsAvailable`; the service returns before any lookup or generation. Tests: `RecoveryWithoutDeliveryTests` (identical responses, spy count 0, no events/file/log of the address, positive control proves the spy counts) |
| F-002 | Startup | Configuration failures surface as unhandled-exception stack traces, messages do not name the setting, and only the administration rate limit is validated. | Yes — startup reliability, production configuration safety | **Fixed** (T009–T018, T021): `StartupConfigurationException` + `StartupFailureReporter` (key-only Critical log, exit 1, no stack trace); every rate-limit group, lifetimes, lockout, request limit, retention, administrator list and key settings validated. Tests: `ProductionStartupValidationTests` (31 process-level cases incl. `Staging`), `StartupFailureReporterTests` |
| F-003 | Health | No readiness endpoint; a database outage cannot be reported. | Yes — startup reliability | Open → T031–T034 |
| F-004 | Bootstrap | No supported way to create the first user: user creation requires a global administrator and the global list only holds ids of existing users. | Yes — startup reliability, production configuration safety | Open → T057–T059 |
| F-005 | Data Protection | Key ring defaults to ephemeral container storage; password-reset credentials are lost on restart and not shared across replicas. | Yes — production configuration safety | **Startup enforcement fixed** (T015, T016: `DataProtection:KeysPath` required in Production-class, validated for absolute path and writability, persisted via the framework file store). Cross-restart credential survival test still pending → T046 |
| F-006 | Forwarded headers | No forwarded-header handling; behind a proxy every client shares one rate-limit bucket. | No — documented behavior with startup warning (direct deployment) | Open → T050, T051 |
| F-008 | Migrations | Two simultaneous `migrate` runs corrupted the outcome: 2 of 3 rounds failed with PostgreSQL `42704` / `2BP01` (the framework's per-call locking was assumed sufficient but is not). | Yes — database migration consistency | **Fixed** (T023/T030): the migrator holds a PostgreSQL advisory lock for the whole run; held lock ⇒ category `locked`, no schema change. Tests: `ConcurrentMigrationTests` (3 rounds both `0`, plus held-lock test) |
| F-009 | Migration script | `dotnet ef` writes the SQL script with a UTF-8 BOM that `psql` rejects on the first statement. | No — operational (DBA path) | **Fixed** (T029): `scripts/generate-migration-script.sh` strips it and verifies no connection settings; applied with real `psql` |
| F-007 | Test warning | CS8619 in `profileImageTests.test.cs:768` (test code only). | No | Open → T035 |

## Sign-off

Open release-blocking items: **F-003 (readiness), F-004 (bootstrap) and the T046 part of F-005**; F-001 and F-002 closed with regression tests (feature in progress).
