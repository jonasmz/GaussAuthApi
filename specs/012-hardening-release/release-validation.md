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
| F-001 | Password recovery | In Production-class environments the only recovery delivery adapter throws, and `RequestRecoveryAsync` generates the reset credential before delivering it and then discards it (FR-018a requires no generation without a channel). | Yes — password/reset secrecy, production configuration safety | Open → fix in T019, test in T020 |
| F-002 | Startup | Configuration failures surface as unhandled-exception stack traces, messages do not name the setting, and only the administration rate limit is validated. | Yes — startup reliability, production configuration safety | Open → T009–T018 |
| F-003 | Health | No readiness endpoint; a database outage cannot be reported. | Yes — startup reliability | Open → T031–T034 |
| F-004 | Bootstrap | No supported way to create the first user: user creation requires a global administrator and the global list only holds ids of existing users. | Yes — startup reliability, production configuration safety | Open → T057–T059 |
| F-005 | Data Protection | Key ring defaults to ephemeral container storage; password-reset credentials are lost on restart and not shared across replicas. | Yes — production configuration safety | Open → T015, T016, T046 |
| F-006 | Forwarded headers | No forwarded-header handling; behind a proxy every client shares one rate-limit bucket. | No — documented behavior with startup warning (direct deployment) | Open → T050, T051 |
| F-007 | Test warning | CS8619 in `profileImageTests.test.cs:768` (test code only). | No | Open → T035 |

## Sign-off

Open release-blocking items: **7 findings recorded; F-001…F-005 are release-blocking and open** (feature in progress).
