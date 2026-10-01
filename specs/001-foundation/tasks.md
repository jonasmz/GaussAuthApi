---
description: "Dependency-ordered implementation tasks for the 001-foundation feature"
---

# Tasks: Executable Service Foundation

**Input**: Design documents from `specs/001-foundation/`

**Prerequisites**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md),
[data-model.md](data-model.md), [operational-api.md](contracts/operational-api.md),
and [quickstart.md](quickstart.md).

**Tests**: The specification explicitly requires essential architecture, startup,
and PostgreSQL migration checks. No coverage target or broad test suite is added.

**Organization**: Tasks are grouped by the three P1 user stories. One
well-contextualized agent executes them sequentially. This constitution
overrides template suggestions to mark independent tasks for parallel work.

## Format: `[ID] [Story] Description`

Every task uses `- [ ] TNNN`; story-phase tasks also carry `[US1]`,
`[US2]`, or `[US3]`. Every task names the files it creates or edits.
No `[P]` markers are used because the constitution prohibits encouraging
parallel agents merely because files differ.

## Path Conventions

Paths are relative to the repository root. Production code lives in
`src/`, focused tests in `tests/`, and development configuration at
the root. `Program.cs` is the composition root with no declared
top-level type; all other C# files use `<name>.<type>.cs` and exactly
one top-level type. EF-generated migration files must be renamed to that
convention without changing migration IDs or generated class identities.

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Create the inspectable .NET 10 solution and test project.

- [ ] T001 Create `GaussAuth.slnx` and `src/GaussAuth.Domain/GaussAuth.Domain.csproj`, `src/GaussAuth.Application/GaussAuth.Application.csproj`, `src/GaussAuth.Infrastructure/GaussAuth.Infrastructure.csproj`, and `src/GaussAuth.Api/GaussAuth.Api.csproj` targeting `net10.0` with nullable references enabled; add no placeholder domain entities or future feature slices.
- [ ] T002 Set and verify project references in `src/GaussAuth.Application/GaussAuth.Application.csproj`, `src/GaussAuth.Infrastructure/GaussAuth.Infrastructure.csproj`, and `src/GaussAuth.Api/GaussAuth.Api.csproj`: Domain has no project/package dependencies, Application references Domain, Infrastructure references Application/Domain, and API references Application/Infrastructure; register all four in `GaussAuth.slnx`.
- [ ] T003 Create `tests/GaussAuth.Foundation.Tests/GaussAuth.Foundation.Tests.csproj` with compatible Microsoft test tooling, MSTest, and `Microsoft.AspNetCore.Mvc.Testing`; add it to `GaussAuth.slnx` and reference the production assemblies needed for architecture and startup checks.

**Checkpoint**: Solution/project boundaries and a focused test harness exist.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Pin only dependencies required by all subsequent validation.

- [ ] T004 Add compatible, pinned EF Core 10, ASP.NET Core Identity EF store, and Npgsql EF Core 10 package references only to `src/GaussAuth.Infrastructure/GaussAuth.Infrastructure.csproj`; add EF design-time package where the selected target/startup tooling requires it; record the choices in `specs/001-foundation/research.md` without adding alternate providers or frameworks.
- [ ] T005 Create `.config/dotnet-tools.json` with a pinned local `dotnet-ef` 10 tool and update root `.gitignore` to exclude `.env`, build outputs, and generated local artifacts while retaining version-controlled migrations; verify `specs/001-foundation/quickstart.md` uses the local tool path.

**Checkpoint**: Dependencies are governed and no user story needs a global tool install.

---

## Phase 3: User Story 1 - Reproduce the development environment (Priority: P1) 🎯 MVP

**Goal**: Operate a dedicated PostgreSQL 17 service and a separate .NET 10
SDK service from the existing development container through the host socket.

**Independent Test**: Check the socket, start/status/stop/recreate PostgreSQL,
run `dotnet --version` in the SDK service, and confirm no host PostgreSQL
or .NET installation is required.

### Implementation for User Story 1

- [ ] T006 [US1] Create `compose.dev.yml` with exactly the `postgres:17` and official .NET 10 `sdk` services, a private Compose network, PostgreSQL health check, repository mount usable by the host daemon, named development database volume, and no published PostgreSQL port; make the SDK service usable both for one-off commands and for running the API.
- [ ] T007 [US1] Create placeholder-only `.env.example` and complete root `.gitignore` so a private `.env` supplies development-only PostgreSQL/database connection values; make missing values fail clearly without exposing them and keep secrets out of Compose files and images.
- [ ] T008 [US1] Execute the service-lifecycle and .NET version scenarios in `specs/001-foundation/quickstart.md` from the existing development container; correct only the documented commands or `compose.dev.yml` as needed, verify the Docker-socket failure path reports its prerequisite, and stop/remove disposable containers after validation.

**Checkpoint**: US1 passes independently; its Docker setup is the MVP
development capability. Do not start API or future services for this check.

---

## Phase 4: User Story 2 - Start and inspect the API foundation (Priority: P1)

**Goal**: Build and start the API with validated configuration, grouped
composition, one safe liveness route, and safe production errors.

**Independent Test**: With a valid external connection setting, API startup
and GET `/health/live` yield HTTP 204 with an empty body; missing/invalid
settings fail without value disclosure, and an injected unexpected error
produces a generic production Problem Details response.

### Tests for User Story 2

- [ ] T009 [US2] Add focused startup/contract tests in `tests/GaussAuth.Foundation.Tests/startupTests.test.cs` using `Microsoft.AspNetCore.Mvc.Testing`: assert GET `/health/live` returns 204 and no body/database details, invalid or absent connection settings fail safely, and a test-only injected exception yields generic HTTP 500 Problem Details without SQL, secret, stack trace, or physical path; do not expose a production test route.

### Implementation for User Story 2

- [ ] T010 [US2] Create `src/GaussAuth.Infrastructure/DependencyInjection/infrastructureServiceCollectionExtensions.extension.cs` with an `AddInfrastructure(...)` registration entry that validates the required external database connection setting without logging its value; leave persistence registration to US3 rather than creating a dummy context or generic repository.
- [ ] T011 [US2] Create `src/GaussAuth.Api/DependencyInjection/applicationServiceCollectionExtensions.extension.cs` and `src/GaussAuth.Api/DependencyInjection/apiServiceCollectionExtensions.extension.cs` with concise `AddApplication(...)` and `AddApiServices(...)` groupings; use only built-in DI, standard structured logging, and built-in Problem Details/exception handling with a safe non-JSON fallback.
- [ ] T012 [US2] Implement `src/GaussAuth.Api/Program.cs` as the readable composition root and only GET `/health/live` from `specs/001-foundation/contracts/operational-api.md`; return 204 with no body or database query, use no functional Identity endpoint, and add `src/GaussAuth.Api/program.entrypoint.cs` only if required to expose Program to the test host.
- [ ] T013 [US2] Run `tests/GaussAuth.Foundation.Tests/startupTests.test.cs` and the API start/operational-check scenario in `specs/001-foundation/quickstart.md`; verify valid startup, safe invalid-configuration failure, and generic production errors, then align the guide with the actual in-container HTTP client.

**Checkpoint**: US2 passes independently after Setup, Foundation, and the
US1 SDK workflow; liveness does not depend on database readiness.

---

## Phase 5: User Story 3 - Prove persistence and architectural boundaries (Priority: P1)

**Goal**: Integrate roleless Identity user storage with PostgreSQL 17,
apply exactly one initial migration, and guard dependency direction.

**Independent Test**: On a clean PostgreSQL 17 container, generate/apply
the migration, reapply without schema drift, resolve Identity user-store
services, and pass architecture and persistence checks.

### Tests for User Story 3

- [ ] T014 [US3] Add `tests/GaussAuth.Foundation.Tests/architectureTests.test.cs` to inspect project/assembly references and fail if Domain depends on ASP.NET Core, Identity, EF Core, PostgreSQL, Docker, Infrastructure, or API, or Application depends on Infrastructure/API; also inspect source and generated migration files for one top-level type per file and `<name>.<type>.cs` names, allowing only top-level-statement `Program.cs`.
- [ ] T015 [US3] Add `tests/GaussAuth.Foundation.Tests/migrationTests.test.cs` to exercise PostgreSQL 17 connectivity, Identity user-store DI resolution, initial migration application, repeat application without new schema changes, and absence of role/application/permission/session/passkey/business tables; run it only against the reproducible development service.

### Implementation for User Story 3

- [ ] T016 [US3] Create `src/GaussAuth.Infrastructure/Persistence/authenticationDbContext.context.cs` using `IdentityUser<Guid>` and roleless `IdentityUserContext<IdentityUser<Guid>, Guid>`. Configure Identity schema Version2, a unique normalized-email index, and the default unique normalized-username index; retain Identity credential, concurrency, and lockout fields without exposing or logging their values. Do not add a Domain User type, passkey tables, or policies for confirmation, MFA, phone, lockout threshold, or lockout duration.
- [ ] T017 [US3] Extend `src/GaussAuth.Infrastructure/DependencyInjection/infrastructureServiceCollectionExtensions.extension.cs` to register the PostgreSQL EF Core provider, `AuthenticationDbContext`, Identity core, and EF user stores with runtime `IdentityOptions.Stores.SchemaVersion` set to Version2 to match the migration model; verify credential/lockout infrastructure resolves while role services, cookies, bearer scheme, token issuance, registration, and login remain absent.
- [ ] T018 [US3] Generate the initial EF Core migration and snapshot under `src/GaussAuth.Infrastructure/Persistence/Migrations/` using `.config/dotnet-tools.json`, Infrastructure as target, and API as startup; inspect and rename generated files to `<name>.<type>.cs` while retaining migration IDs, and confirm only required user-side Identity schema plus EF history are present.
- [ ] T019 [US3] Apply and reapply the migration from the SDK container, then run `tests/GaussAuth.Foundation.Tests/architectureTests.test.cs` and `tests/GaussAuth.Foundation.Tests/migrationTests.test.cs`; confirm the unique normalized-email index, schema Version2, database connectivity, and no cross-application/global-role or passkey schema.

**Checkpoint**: US3 passes and the foundation is ready for `002-users-profiles`
without adding a Domain User or speculative slice in this feature.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Validate the complete foundation and remove accidental scope.

- [ ] T020 Review `GaussAuth.slnx`, every `src/**/*.csproj`, `src/**/*.cs`, and `tests/GaussAuth.Foundation.Tests/*.cs` for the constitutional reference direction, nullable setting, one-type/filename convention, and absence of empty feature stubs, unnecessary packages, and global Identity roles; fix only concrete violations.
- [ ] T021 Run every command in `specs/001-foundation/quickstart.md` from the existing development container, including build, health, migration, repeat migration, tests, stop, and intentional clean recreation; update `specs/001-foundation/quickstart.md` to the verified commands and expected outcomes.
- [ ] T022 Review `compose.dev.yml`, `.env.example`, root `.gitignore`, `src/GaussAuth.Api/Program.cs`, and the test output for committed secrets, sensitive logs/responses, unnecessary public routes or containers, and accidental selection of token, password, email, or production deployment policy; correct any findings.
- [ ] T023 Perform the final clean build and essential tests with `GaussAuth.slnx` from the SDK container, confirm the sole public route matches `specs/001-foundation/contracts/operational-api.md`, and stop/remove disposable services with the lifecycle command in `specs/001-foundation/quickstart.md`.

---

## Dependencies & Execution Order

### Phase Dependencies

- Phase 1 → Phase 2 → US1 → US2 → US3 → Polish.
- T006 requires T001–T005; T008 requires T006–T007.
- T009 requires the test project and SDK workflow; T010–T012 implement
  the behaviors it checks; T013 requires T009–T012.
- T014–T015 define US3 checks; T016–T018 implement them; T019 requires
  T014–T018 and a healthy PostgreSQL 17 service.
- T020–T023 require all three story checkpoints.

### User Story Dependencies

- **US1**: No other story dependency; independently proves container lifecycle.
- **US2**: Uses US1's SDK execution path but has an independent API
  startup/liveness/error check; it does not require a migrated database.
- **US3**: Uses US1's database and US2's API startup project for EF tooling;
  independently proves migration and architectural rules.

### Within Each User Story

- Write focused tests before the behavior they verify where the spec
  requests tests; tests that cannot compile before the API entry point or
  context exists are pending, not evidence of a behavioral failure.
  Implement the missing behavior and rerun them.
- Establish models/configuration before registrations and API integration.
- Complete each story checkpoint before starting the next story.
- Preserve source and migration filenames when generated files are reviewed.

### Parallel Execution Examples

None. The constitution requires one well-contextualized sequential agent
unless explicit task-specific justification outweighs the token and overlap
cost. No task in this feature has that justification, so no `[P]` marker or
parallel-agent example is supplied.

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Setup and Foundational phases.
2. Complete US1 and validate the Docker socket, PostgreSQL 17 lifecycle,
   and .NET 10 SDK container independently.
3. Continue to US2 and US3 only after the US1 checkpoint passes.

### Incremental Delivery

1. US1 makes the development environment reproducible.
2. US2 adds an executable API with one safe liveness contract.
3. US3 adds Identity persistence and architecture/migration proof.
4. Polish validates the full quickstart and removes accidental scope.

No production deployment or future identity workflow is included.

## Notes

- Task IDs are strictly sequential and every story-phase task has its
  story label and a concrete path.
- Testing is limited to the architectural, startup, migration, and
  security behaviors identified in the specification.
- A single agent should finish each checkpoint before moving on;
  independent files alone do not authorize parallel execution.
