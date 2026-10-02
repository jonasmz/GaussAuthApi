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

Filled by T025–T030 (from zero, upgrade with data, command diagnostics, safety review, script equivalence, concurrency).

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
| F-007 | Test warning | CS8619 in `profileImageTests.test.cs:768` (test code only). | No | Open → T035 |

## Sign-off

Open release-blocking items: **F-003 (readiness), F-004 (bootstrap) and the T046 part of F-005**; F-001 and F-002 closed with regression tests (feature in progress).
