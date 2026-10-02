# Implementation Plan: Hardening and Release Readiness

**Branch**: `012-hardening-release` | **Date**: 2026-10-02 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/012-hardening-release/spec.md`

## Summary

Make the assembled platform (features 001–011) releasable without adding product functionality. The work is a verification pass plus targeted hardening of eight concrete gaps found by reading the current code (see [research.md](research.md)):

1. **Startup/config** — consolidate scattered eager checks into typed, fail-fast validation with setting-naming, secret-free messages and no stack-trace dump; make "Production-class" (anything other than Development/Testing) the single rule for unsafe-default gating.
2. **Health** — keep liveness, add a minimal unauthenticated readiness check (database reachable, schema current, profile storage usable) using the built-in ASP.NET Core health-check infrastructure.
3. **Network edge** — safe forwarded-header handling (explicit trusted proxies only), HSTS limited to Production-class, no `Server` header, CORS stays denied, all rate-limit values validated, endpoint rate-limit coverage test.
4. **Recovery in Production** — `IRecoveryDelivery` gains an availability signal so no reset credential is generated when no channel exists (FR-018a); no new delivery mechanism.
5. **Persistence requirements** — Production must declare profile-storage persistence **and must configure a persistent Data Protection key-ring location** (mandatory in Production-class, optional in Development/Testing) so password-reset credentials and other protected state survive restarts and replicas.
6. **First-administrator bootstrap** — the platform currently cannot be bootstrapped in production (creating a user requires a global administrator, and the global list only holds UserIds of existing users). A one-off `bootstrap-admin` command in the same image creates the first user through the existing creation rules and prints its UserId; authority still comes only from the configured list (empty list ⇒ none).
7. **Migrations** — one-off `migrate` command in the same image (no auto-migrate at runtime) plus an idempotent SQL script generated in the same build; validation from zero and from the 009 schema state with data.
8. **Runtime package + docs** — production `Dockerfile`, `compose.release.yml` for evaluation, operator documentation set under `docs/`, secret-hygiene scan, and a recorded release-validation log (full Release build + regression run).

No new NuGet packages. No schema changes (no new migration) are planned; if the review finds a release-blocking migration defect, a corrective migration is the only permitted exception.

## Technical Context

**Language/Version**: C# on .NET 10 (`net10.0`)

**Primary Dependencies**: ASP.NET Core (shared framework, including built-in health checks, rate limiting, forwarded-headers, HSTS, Data Protection), ASP.NET Core Identity, EF Core 10 + Npgsql provider, SkiaSharp (existing, profile images). **No new packages.**

**Storage**: PostgreSQL 17 (EF Core migrations); local filesystem for profile images (`ProfileImages:RootPath`); filesystem key ring for Data Protection (required in Production-class)

**Testing**: MSTest (`tests/GaussAuth.Foundation.Tests`), `WebApplicationFactory<Program>`, real PostgreSQL 17 from the development compose; new throwaway-database tests for migration-from-zero/upgrade

**Target Platform**: Linux container (Docker); `mcr.microsoft.com/dotnet/aspnet:10.0` runtime image, `sdk:10.0` build stage

**Project Type**: Web service (ports-and-adapters .NET solution: Domain / Application / Infrastructure / Api)

**Performance Goals**: Not a goal of this feature (load/capacity out of scope). Only operational: readiness reflects dependency loss/recovery within 10 s (SC-006); readiness cannot be used to amplify load on the database (short result cache).

**Constraints**: No new functional capability; no redesign; no external observability or secret-management product; no new dependencies; one top-level type per C# file named `<name>.<type>.cs`; .NET commands run in the sdk container; liveness must not touch external systems; health responses must be empty-bodied.

**Scale/Scope**: Cross-cutting: ~18 small C# source files, 1 Dockerfile, 1 compose file, 2 scripts, 6 operator documents plus a docs index, ~15 focused test files and 2 test fixtures. Verification touches all 11 prior features.

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
| VII. Simplicity & dependency governance | PASS. The `bootstrap-admin` command is a correction of a release-blocking startup-reliability defect, reuses the existing create-user use case, adds no endpoint and no dependency, and is explicitly operator-run. Built-in .NET health checks, options validation, `ForwardedHeaders`, Data Protection used; no packages added. New infrastructure (Dockerfile/compose) is justified by FR-040/042 and uses only Microsoft base images already implied by the stack. Subcommand `migrate` chosen over a separate tool/bundle for fewer moving parts (research R4). |
| VIII. Proportionate testing / error contracts | PASS. Tests target release-blocking categories only; no coverage padding. Error contract unchanged (RFC 7807 problem details). |
| IX. Spec-driven development | PASS. Plan derives from spec; three spec-level discoveries (recovery adapter, Data Protection key ring, first-administrator bootstrap) are documented in research, confirmed with the project owner, and written back into the spec (FR-018a, FR-041a, FR-044–046). |
| Operational constraints | PASS. Rate limits, request/upload limits, structured logging, external configuration, no secrets in images all addressed. |
| One type per file / naming | PASS. Tasks follow `<name>.<type>.cs` and reuse the existing suffix vocabulary (`service`, `extension`, `options`, `adapter`, `handler`, `fixture`, `test`). Three suffixes are introduced deliberately because no existing one fits their category — `exception` (the startup configuration exception), `check` (health-check implementations) and `factory` (the EF design-time factory); both are named after the framework concept they implement. Subcommand dispatcher and command hosts use `service`. |

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
│   ├── bootstrap-command.md
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
(also: artifacts/ added to .gitignore; .env.release and *.pem added to .gitignore; .env.example and compose.dev.yml comments updated)
docs/
├── deployment.md                                  # runtime, compose, proxy/HTTPS, health, shutdown
├── configuration.md                               # reference + secrets + environments
├── database.md                                    # migrations, upgrade, backup/restore, profile storage, key ring
├── bootstrap.md                                   # first global administrator procedure
├── security-baseline.md                           # HTTP baseline, CORS, rate limits, consumer secrets, crypto, logging
├── limitations.md                                 # known operational limitations
└── README.md                                      # index of the six documents

src/GaussAuth.Application/Passwords/
├── Ports/recoveryDelivery.interface.cs            # UPDATED: + availability member
└── passwordManagementService.service.cs           # UPDATED: no credential generation when unavailable

src/GaussAuth.Infrastructure/
├── Configuration/
│   ├── startupConfigurationException.exception.cs # NEW: setting-naming, value-free
│   ├── productionClassEnvironment.extension.cs    # NEW: Production-class rule (R1)
│   ├── keyRingOptions.options.cs                  # NEW: key-ring path (required in Production-class)
│   ├── keyRingOptions.validator.cs                # NEW
│   ├── configurationValueReader.extension.cs      # NEW: value-free typed/bounded configuration reads
│   └── startupDiagnostics.service.cs              # NEW: startup/shutdown summary + Production-class warnings
├── Health/
│   ├── databaseReadiness.check.cs                 # NEW: reachable + schema current
│   └── profileStorageReadiness.check.cs           # NEW: root usable
├── Persistence/databaseMigrator.service.cs        # NEW: shared by `migrate` command (beside the repositories; not in the generated Migrations folder)
├── Persistence/databaseMigration.result.cs        # NEW: outcome (success / failure category + migration id)
├── Persistence/authenticationDatabaseConnection.service.cs # NEW: validated, never-echoed connection string read (shared)
├── Persistence/authenticationDbContextDesignTime.factory.cs # NEW: design-time factory so SQL generation needs no API settings
├── DependencyInjection/persistenceServiceCollectionExtensions.extension.cs # NEW: one DbContext+Identity registration shared by API and `migrate`
├── Bootstrap/firstAdministratorBootstrap.service.cs # NEW: guarded first-user creation (reuses create-user use case)
├── Bootstrap/firstAdministratorBootstrap.result.cs  # NEW: outcome category + UserId
├── Passwords/protectedFileRecoveryDelivery.service.cs  # UPDATED: availability
├── ProfileImages/profileImagesOptions.options.cs  # UPDATED: persistence declaration
├── Sessions/accessCredentialSigningKey.service.cs # UPDATED: setting-naming messages
└── DependencyInjection/infrastructureServiceCollectionExtensions.extension.cs  # UPDATED

src/GaussAuth.Api/
├── Program.cs / program.entrypoint.cs             # UPDATED: startup failure handling, migrate subcommand, pipeline order
├── appsettings.json                               # NEW: safe logging defaults, no secrets
├── appsettings.Production.json                    # NEW: JSON console, quieter levels
├── appsettings.Development.json                   # NEW: verbose
├── Startup/startupFailureReporter.service.cs       # NEW: value-free Critical log + exit code for startup configuration failures
├── Commands/commandLine.service.cs                 # NEW: subcommand dispatcher (`migrate`, `bootstrap-admin`)
├── Commands/migrateCommand.service.cs              # NEW: `migrate` subcommand host
├── Commands/bootstrapAdminCommand.service.cs       # NEW: `bootstrap-admin` subcommand host
└── DependencyInjection/
    ├── forwardedHeadersSetup.extension.cs         # NEW: explicit trusted proxies only
    ├── forwardedHeadersOptions.options.cs         # NEW
    ├── forwardedHeadersOptions.validator.cs       # NEW
    ├── rateLimitPolicy.options.cs                 # NEW: one group's PermitLimit/Window
    ├── rateLimitOptions.options.cs                # NEW: validated per-group limits
    ├── rateLimitOptions.validator.cs              # NEW
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
├── countingPasswordCredentialService.fixture.cs   # NEW: spy proving no credential is generated
├── testHostEnvironment.fixture.cs                 # NEW: IHostEnvironment with a chosen name
├── throwawayDatabase.fixture.cs                   # NEW: uniquely named empty database per test
├── throwawayDatabaseTests.test.cs                 # NEW: fixture sanity (created empty, unique, dropped)
├── startupFailureReporterTests.test.cs            # NEW: reporter output is key-only, no stack trace
├── apiProcess.fixture.cs                          # NEW: process launcher (extracted from startupTests)
├── releaseMigrationValidationTests.test.cs        # NEW (from zero, throwaway DB)
├── releaseMigrationUpgradeTests.test.cs           # NEW (previous state + data → latest)
├── releaseMigrationScriptTests.test.cs            # NEW (idempotent SQL equals the command)
├── concurrentMigrationTests.test.cs               # NEW (two simultaneous migrators; held lock)
├── databaseSchema.fixture.cs                      # NEW: schema introspection/comparison
├── migrationServices.fixture.cs                   # NEW: shared persistence registration for tests
├── productionApiEnvironment.fixture.cs            # NEW: complete valid Production-class environment
├── migrateCommandTests.test.cs                    # NEW (success, value-free failure, web host never migrates)
├── cryptographicConfigurationTests.test.cs        # NEW
├── gracefulShutdownTests.test.cs                  # NEW
├── dataProtectionPersistenceTests.test.cs         # NEW
├── sensitiveLoggingTests.test.cs                  # NEW
├── correlationTests.test.cs                       # NEW
├── bootstrapAdministratorTests.test.cs            # NEW (refuses when users exist, no secret output, no authority by itself)
├── repositorySecretHygieneTests.test.cs           # NEW
└── (existing suites)                              # run unchanged as regression
```

**Structure Decision**: Keep the existing four-project solution. Cross-cutting runtime concerns that need infrastructure types go in `GaussAuth.Infrastructure` (new `Configuration`, `Health`, `Migrations` folders); HTTP-pipeline concerns go in `GaussAuth.Api/DependencyInjection`. Operator-facing material goes in a new top-level `docs/` and `scripts/`, with `Dockerfile` and `compose.release.yml` at the repository root beside the existing `compose.dev.yml` (which is not modified).

## Complexity Tracking

No constitution violations to justify.
