# Implementation Plan: Hardening and Release Readiness

**Branch**: `012-hardening-release` | **Date**: 2026-10-02 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/012-hardening-release/spec.md`

## Summary

Make the assembled platform (features 001–011) releasable without adding functional capability. The work is a verification pass plus targeted hardening of seven concrete gaps found by reading the current code (see [research.md](research.md)):

1. **Startup/config** — consolidate scattered eager checks into typed, fail-fast validation with setting-naming, secret-free messages and no stack-trace dump; make "Production-class" (anything other than Development/Testing) the single rule for unsafe-default gating.
2. **Health** — keep liveness, add a minimal unauthenticated readiness check (database reachable, schema current, profile storage usable) using the built-in ASP.NET Core health-check infrastructure.
3. **Network edge** — safe forwarded-header handling (explicit trusted proxies only), HSTS limited to Production-class, no `Server` header, CORS stays denied, all rate-limit values validated, endpoint rate-limit coverage test.
4. **Recovery in Production** — `IRecoveryDelivery` gains an availability signal so no reset credential is generated when no channel exists (FR-018a); no new delivery mechanism.
5. **Persistence declarations** — Production must declare profile-storage persistence; optional persistent Data Protection key ring with a warning when absent.
6. **Migrations** — one-off `migrate` command in the same image (no auto-migrate at runtime) plus an idempotent SQL script generated in the same build; validation from zero and from the 009 schema state with data.
7. **Runtime package + docs** — production `Dockerfile`, `compose.release.yml` for evaluation, operator documentation set under `docs/`, secret-hygiene scan, and a recorded release-validation log (full Release build + regression run).

No new NuGet packages. No schema changes (no new migration) are planned; if the review finds a release-blocking migration defect, a corrective migration is the only permitted exception.

## Technical Context

**Language/Version**: C# on .NET 10 (`net10.0`)

**Primary Dependencies**: ASP.NET Core (shared framework, including built-in health checks, rate limiting, forwarded-headers, HSTS, Data Protection), ASP.NET Core Identity, EF Core 10 + Npgsql provider, SkiaSharp (existing, profile images). **No new packages.**

**Storage**: PostgreSQL 17 (EF Core migrations); local filesystem for profile images (`ProfileImages:RootPath`); optional filesystem key ring for Data Protection

**Testing**: MSTest (`tests/GaussAuth.Foundation.Tests`), `WebApplicationFactory<Program>`, real PostgreSQL 17 from the development compose; new throwaway-database tests for migration-from-zero/upgrade

**Target Platform**: Linux container (Docker); `mcr.microsoft.com/dotnet/aspnet:10.0` runtime image, `sdk:10.0` build stage

**Project Type**: Web service (ports-and-adapters .NET solution: Domain / Application / Infrastructure / Api)

**Performance Goals**: Not a goal of this feature (load/capacity out of scope). Only operational: readiness reflects dependency loss/recovery within 10 s (SC-006); readiness cannot be used to amplify load on the database (short result cache).

**Constraints**: No new functional capability; no redesign; no external observability or secret-management product; no new dependencies; one top-level type per C# file named `<name>.<type>.cs`; .NET commands run in the sdk container; liveness must not touch external systems; health responses must be empty-bodied.

**Scale/Scope**: Cross-cutting: ~10 small C# files, 1 Dockerfile, 1 compose file, 2 scripts, 5 operator documents, ~8 focused test files. Verification touches all 11 prior features.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design — see end of section.*

| Principle | Assessment |
|-----------|------------|
| I. Bounded Authentication Service | PASS. No business concepts introduced; consumers still integrate only through contracts. |
| II. Hexagonal / vertical slices | PASS. Readiness checks and options validators live in Infrastructure/Api; the only Application change is one member on the existing `IRecoveryDelivery` port. Domain untouched. |
| III. Identity & Application isolation | PASS. Verified, not changed; regression tests cover isolation. |
| IV. Roles, permissions, sessions | PASS. Verified; session freshness policy from 006 unchanged. |
| V. Fixed technology & persistence | PASS. .NET 10, EF Core, PostgreSQL 17, versioned migrations only. Development container rules unchanged. **Constitution "undecided" list:** this plan explicitly resolves *production containerization* = Docker image + evaluation compose (as the spec Assumptions state). *Deployment platform* and *reverse proxy product* remain undecided; only a behavioral proxy contract (trusted-proxy configuration) is defined. No amendment required — the constitution permits resolution by a plan. |
| VI. Security by design | PASS / strengthens. Secret-free startup errors, safe forwarded headers, no stack traces, no secrets in logs, Production-class gating. |
| VII. Simplicity & dependency governance | PASS. Built-in .NET health checks, options validation, `ForwardedHeaders`, Data Protection used; no packages added. New infrastructure (Dockerfile/compose) is justified by FR-040/042 and uses only Microsoft base images already implied by the stack. Subcommand `migrate` chosen over a separate tool/bundle for fewer moving parts (research R4). |
| VIII. Proportionate testing / error contracts | PASS. Tests target release-blocking categories only; no coverage padding. Error contract unchanged (RFC 7807 problem details). |
| IX. Spec-driven development | PASS. Plan derives from spec; two spec-level discoveries (recovery adapter, Data Protection keys) are documented in research and either map to existing FRs (FR-018a) or are declared optional operational hardening. |
| Operational constraints | PASS. Rate limits, request/upload limits, structured logging, external configuration, no secrets in images all addressed. |
| One type per file / naming | PASS by construction; tasks must follow `<name>.<type>.cs`. |

**Gate result**: PASS — no violations, Complexity Tracking not required.

**Post-design re-check**: PASS. Design adds no dependencies, no layer violations, and no new persisted concepts.

## Project Structure

### Documentation (this feature)

```text
specs/012-hardening-release/
├── plan.md                         # This file
├── research.md                     # Phase 0: findings and decisions (R1–R21)
├── data-model.md                   # Phase 1: configuration/health/migration/validation-record model
├── quickstart.md                   # Phase 1: runnable release-validation scenarios
├── contracts/
│   ├── health-endpoints.md
│   ├── migration-command.md
│   ├── configuration-reference.md
│   └── runtime-package.md
├── checklists/requirements.md
├── release-validation.md           # Created during implementation: the Release Validation Record
└── tasks.md                        # Phase 2 ($speckit-tasks — NOT created here)
```

### Source Code (repository root)

```text
Dockerfile                                         # NEW production image (multi-stage, non-root)
compose.release.yml                                # NEW local/integration composition (migrate → api, volumes)
.env.release.example                               # NEW placeholders only
.dockerignore                                      # UPDATED (exclude specs, tests, .git, .env*)
scripts/
├── generate-migration-script.sh                   # NEW idempotent SQL from the same build
└── scan-secrets.sh                                # NEW repository secret-hygiene scan
docs/
├── deployment.md                                  # runtime, compose, proxy/HTTPS, health, shutdown
├── configuration.md                               # reference + secrets + environments
├── database.md                                    # migrations, upgrade, backup/restore, profile storage
├── security-baseline.md                           # HTTP baseline, CORS, rate limits, consumer secrets, crypto, logging
└── limitations.md                                 # known operational limitations

src/GaussAuth.Application/Passwords/
├── Ports/recoveryDelivery.interface.cs            # UPDATED: + availability member
└── passwordManagementService.service.cs           # UPDATED: no credential generation when unavailable

src/GaussAuth.Infrastructure/
├── Configuration/
│   ├── startupConfigurationException.exception.cs # NEW: setting-naming, value-free
│   ├── productionClassEnvironment.extension.cs    # NEW: Production-class rule (R1)
│   └── dataProtectionOptions.options.cs           # NEW: optional key-ring path + warning
├── Health/
│   ├── databaseReadiness.check.cs                 # NEW: reachable + schema current
│   └── profileStorageReadiness.check.cs           # NEW: root usable
├── Migrations/databaseMigrator.service.cs         # NEW: shared by `migrate` command
├── Passwords/protectedFileRecoveryDelivery.service.cs  # UPDATED: availability
├── ProfileImages/profileImagesOptions.options.cs  # UPDATED: persistence declaration
├── Sessions/accessCredentialSigningKey.service.cs # UPDATED: setting-naming messages
└── DependencyInjection/infrastructureServiceCollectionExtensions.extension.cs  # UPDATED

src/GaussAuth.Api/
├── Program.cs / program.entrypoint.cs             # UPDATED: startup failure handling, migrate subcommand, pipeline order
├── appsettings.json                               # NEW: safe logging defaults, no secrets
├── appsettings.Production.json                    # NEW: JSON console, quieter levels
├── appsettings.Development.json                   # NEW: verbose
├── Migrations/migrateCommand.command.cs           # NEW: `migrate` subcommand host
└── DependencyInjection/
    ├── forwardedHeadersSetup.extension.cs         # NEW: explicit trusted proxies only
    ├── rateLimitOptions.options.cs                # NEW: validated per-group limits
    ├── readinessEndpoints.extension.cs            # NEW: /health/ready
    ├── apiSecurityHeaders.extension.cs            # UPDATED if needed
    └── safeExceptionHandler.handler.cs            # UPDATED: log exception server-side

tests/GaussAuth.Foundation.Tests/
├── productionStartupValidationTests.test.cs       # NEW
├── readinessTests.test.cs                         # NEW
├── forwardedHeadersTests.test.cs                  # NEW
├── httpBaselineTests.test.cs                      # NEW (headers, CORS denied, limits, error shape)
├── rateLimitCoverageTests.test.cs                 # NEW (every endpoint limited or exempt-by-rationale)
├── recoveryWithoutDeliveryTests.test.cs           # NEW
├── releaseMigrationValidationTests.test.cs        # NEW (zero + upgrade-with-data, throwaway DBs)
├── repositorySecretHygieneTests.test.cs           # NEW
└── (existing suites)                              # run unchanged as regression
```

**Structure Decision**: Keep the existing four-project solution. Cross-cutting runtime concerns that need infrastructure types go in `GaussAuth.Infrastructure` (new `Configuration`, `Health`, `Migrations` folders); HTTP-pipeline concerns go in `GaussAuth.Api/DependencyInjection`. Operator-facing material goes in a new top-level `docs/` and `scripts/`, with `Dockerfile` and `compose.release.yml` at the repository root beside the existing `compose.dev.yml` (which is not modified).

## Complexity Tracking

No constitution violations to justify.
