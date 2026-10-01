---

description: "Actionable implementation tasks for applications and memberships"
---

# Tasks: Applications and Memberships

**Input**: Design documents from `specs/003-applications-memberships/`

**Prerequisites**: [plan.md](plan.md), [spec.md](spec.md),
[research.md](research.md), [data-model.md](data-model.md), and
[applications-memberships-api.md](contracts/applications-memberships-api.md)

**Tests**: Essential tests are included because the specification explicitly
requires lifecycle, uniqueness, isolation, inactive-parent, persistence, and
architecture coverage. Run them inside the existing `sdk` Docker service.

**Organization**: Tasks are grouped by user story after a small shared
foundation. Execute sequentially: the constitution does not permit parallel
implementation merely because files differ, and the shared DbContext/migration
must remain coherent.

## Phase 1: Setup

**Purpose**: Confirm the established feature-002 baseline before making
additive changes.

- [X] T001 Review `specs/003-applications-memberships/spec.md`, `plan.md`, `data-model.md`, and `contracts/applications-memberships-api.md` and record no deviations from the clarified lifecycle and isolation rules.
- [X] T002 Inspect `src/GaussAuth.Infrastructure/Persistence/authenticationDbContext.context.cs`, `src/GaussAuth.Api/program.entrypoint.cs`, `src/GaussAuth.Api/DependencyInjection/applicationServiceCollectionExtensions.extension.cs`, and `src/GaussAuth.Infrastructure/DependencyInjection/infrastructureServiceCollectionExtensions.extension.cs` to preserve existing registration and mapping conventions.

---

## Phase 2: Foundational Domain, Persistence, and Composition

**Purpose**: Establish the shared framework-independent model and the one
authoritative persistence path required by all user stories.

**⚠️ CRITICAL**: Complete this phase before endpoint/user-story work.

- [X] T003 Create framework-independent `Application` with stable `Guid` id, immutable canonical code, immutable name, `IsActive`, UTC timestamps, and idempotent activation/deactivation in `src/GaussAuth.Domain/Applications/application.entity.cs`.
- [X] T004 Create framework-independent `ApplicationMembership` with stable `Guid` id, immutable non-empty `UserId`/`ApplicationId`, `IsActive`, UTC timestamps, and idempotent activation/deactivation in `src/GaussAuth.Domain/Memberships/applicationMembership.entity.cs`.
- [ ] T005 Define targeted application reads/add/list/save and duplicate-safe save semantics in `src/GaussAuth.Application/Applications/Ports/applicationRepository.interface.cs`.
- [ ] T006 Define pair/context reads, lists by one user/application, add/save, and duplicate-safe save semantics in `src/GaussAuth.Application/Memberships/Ports/applicationMembershipRepository.interface.cs`.
- [X] T007 Extend `src/GaussAuth.Infrastructure/Persistence/authenticationDbContext.context.cs` with `Applications` and `ApplicationMemberships` mappings: `Code` required max 64 with unique `IX_Applications_Code`; `Name` required max 200; required membership ids; unique `IX_ApplicationMemberships_UserId_ApplicationId`; restrictive FKs to `Users.Id` and `Applications.Id`; never add EF attributes to Domain types.
- [ ] T008 Implement the `IApplicationRepository` adapter, including translation of only `IX_Applications_Code` PostgreSQL unique violations to the expected duplicate outcome, in `src/GaussAuth.Infrastructure/Persistence/applicationRepository.repository.cs`.
- [ ] T009 Implement the `IApplicationMembershipRepository` adapter, including translation of only `IX_ApplicationMemberships_UserId_ApplicationId` PostgreSQL unique violations to the expected duplicate outcome, in `src/GaussAuth.Infrastructure/Persistence/applicationMembershipRepository.repository.cs`.
- [X] T010 Register the two feature ports/adapters with the existing scoped DbContext in `src/GaussAuth.Infrastructure/DependencyInjection/infrastructureServiceCollectionExtensions.extension.cs` and preserve existing registrations.
- [X] T011 Create one EF Core migration and update the model snapshot under `src/GaussAuth.Infrastructure/Persistence/Migrations/` for exactly `Applications` and `ApplicationMemberships`, their named unique indexes, and restrictive foreign keys; run migration generation only inside the `sdk` container.
- [X] T012 Update schema/migration expectations for the new tables, named indexes, foreign keys, and repeated migration application in `tests/GaussAuth.Foundation.Tests/usersSchemaMigrationTests.test.cs`.

**Checkpoint**: Domain remains framework-independent, PostgreSQL is the
authoritative uniqueness/referential-integrity layer, and the migration applies
cleanly in the existing Docker environment.

---

## Phase 3: User Story 1 - Register and Manage a Consuming Application (Priority: P1) 🎯 MVP

**Goal**: Create, retrieve, list, activate, and deactivate a stable consuming
application without modelling its business domain.

**Independent Test**: Register a valid unique application; retrieve it by id
and normalized code; list it; transition both states; verify a duplicate code
is rejected and repeated transitions are no-ops.

### Tests for User Story 1

- [ ] T013 [US1] Add integration tests for successful application creation, lowercase/trim code normalization, `201` location, `400` invalid code/name, and sequential/concurrent duplicate-code `409` behavior in `tests/GaussAuth.Foundation.Tests/applicationsTests.test.cs`.
- [ ] T014 [US1] Add integration tests for get-by-id, get-by-code, cursor/limit application listing (default 50; valid 1–100; invalid limit `400`), activate/deactivate, idempotency, timestamp behavior, and safe `404` results in `tests/GaussAuth.Foundation.Tests/applicationsTests.test.cs`.

### Implementation for User Story 1

- [ ] T015 [US1] Add create-application command, focused result, and handler in `src/GaussAuth.Application/Applications/CreateApplication/createApplication.command.cs`, `createApplication.result.cs`, and `createApplication.handler.cs`; enforce code “3–64 ASCII letters/digits/single hyphens, begins/ends alphanumeric” after trim/lowercase and name “trimmed, 1–200 non-whitespace chars,” then log only application id/outcome.
- [ ] T016 [US1] Add get-by-id/get-by-code queries and handlers in `src/GaussAuth.Application/Applications/GetApplication/` and a cursor/limit list query/handler in `src/GaussAuth.Application/Applications/ListApplications/` (default 50, valid limit 1–100, stable ascending `Id` order, opaque continuation after the final id), with no broad search/reporting behavior.
- [ ] T017 [US1] Add idempotent lifecycle handlers in `src/GaussAuth.Application/Applications/ActivateApplication/activateApplication.handler.cs` and `src/GaussAuth.Application/Applications/DeactivateApplication/deactivateApplication.handler.cs`; preserve historical records and never delete memberships.
- [ ] T018 [US1] Create request/response DTOs with Data Annotations and safe domain mapping in `src/GaussAuth.Api/Applications/createApplicationRequest.dto.cs` and `src/GaussAuth.Api/Applications/applicationResponse.dto.cs`.
- [ ] T019 [US1] Implement `POST /applications`, `GET /applications/{id:guid}`, `GET /applications/by-code/{code}`, paged `GET /applications`, and state routes with 400/404/409 Problem Details in `src/GaussAuth.Api/Applications/applicationsEndpoints.extension.cs`.
- [ ] T020 [US1] Register application handlers in `src/GaussAuth.Api/DependencyInjection/applicationServiceCollectionExtensions.extension.cs` and map `MapApplicationsEndpoints()` in `src/GaussAuth.Api/program.entrypoint.cs` without adding caller authentication or a role model.

**Checkpoint**: Application registration and lifecycle work independently and
the P1 application-context MVP is demonstrable.

---

## Phase 4: User Story 2 - Associate a Global User with an Application (Priority: P1)

**Goal**: Create one durable, unique membership for an existing global user
and an explicitly identified active application.

**Independent Test**: With a feature-002 user and application, create and
retrieve a membership; prove duplicate, missing-parent, inactive-application,
and inactive-user rules produce the specified safe outcomes.

### Tests for User Story 2

- [ ] T021 [US2] Add integration tests for active-user/active-application membership `201`, pair retrieval, missing user/application `404`, duplicate sequential/concurrent `409`, database retention of exactly one pair, and both cursor/limit membership lists (default 50; valid 1–100; invalid limit/cursor `400`) in `tests/GaussAuth.Foundation.Tests/applicationMembershipsTests.test.cs`.
- [ ] T022 [US2] Add integration tests proving inactive application rejects every new membership and inactive user plus active application creates one inactive historical membership in `tests/GaussAuth.Foundation.Tests/applicationMembershipsTests.test.cs`.

### Implementation for User Story 2

- [ ] T023 [US2] Add create-membership command, result, and handler in `src/GaussAuth.Application/Memberships/CreateMembership/createMembership.command.cs`, `createMembership.result.cs`, and `createMembership.handler.cs`; load the existing global User and Application, reject inactive application, create active only when user is active, and never create a missing parent.
- [ ] T024 [US2] Add pair retrieval query/handler in `src/GaussAuth.Application/Memberships/GetMembership/` and cursor/limit user/application list queries/handlers in `src/GaussAuth.Application/Memberships/ListMemberships/` (default 50, valid limit 1–100, stable ascending `Id` order, opaque continuation after the final id); results must retain both ids and must not claim cross-application eligibility.
- [ ] T025 [US2] Create membership request/response DTOs in `src/GaussAuth.Api/Memberships/createMembershipRequest.dto.cs` and `src/GaussAuth.Api/Memberships/applicationMembershipResponse.dto.cs`; require non-empty `userId` and expose only ids, state, and timestamps.
- [ ] T026 [US2] Implement explicit-context membership create, pair retrieval, and paged application/user list routes with 400/404/409 contracts in `src/GaussAuth.Api/Memberships/membershipsEndpoints.extension.cs`.
- [ ] T027 [US2] Register membership handlers in `src/GaussAuth.Api/DependencyInjection/applicationServiceCollectionExtensions.extension.cs` and map `MapMembershipsEndpoints()` in `src/GaussAuth.Api/program.entrypoint.cs`.

**Checkpoint**: Membership creation is persistent, unique, referentially
sound, and explicitly scoped to an application.

---

## Phase 5: User Story 3 - Manage Membership Eligibility Independently (Priority: P2)

**Goal**: Activate/deactivate an individual membership without changing other
memberships, the global user, or the application.

**Independent Test**: Assign one user to two applications, change one
membership, and verify the other remains unchanged; verify inactive user or
application prevents activation without cascading membership state changes.

### Tests for User Story 3

- [ ] T028 [US3] Add integration tests for membership activation/deactivation, repeated idempotent transitions, safe `404`, and `409` activation failures for inactive user or application that leave the membership state unchanged in `tests/GaussAuth.Foundation.Tests/applicationMembershipsTests.test.cs`.
- [ ] T029 [US3] Add integration tests for two memberships of one user, one-membership-only state changes, no cross-application inference, and parent deactivation preserving membership `IsActive` in `tests/GaussAuth.Foundation.Tests/applicationMembershipsTests.test.cs`.

### Implementation for User Story 3

- [ ] T030 [US3] Add activate-membership handler in `src/GaussAuth.Application/Memberships/ActivateMembership/activateMembership.handler.cs`; require existing membership/user/application and active user/application, return explicit inactive-parent conflicts, and leave membership unchanged on rejection.
- [ ] T031 [US3] Add idempotent deactivate-membership handler in `src/GaussAuth.Application/Memberships/DeactivateMembership/deactivateMembership.handler.cs`; change only the selected membership and log ids/outcome without personal data.
- [ ] T032 [US3] Add application-context activate/deactivate membership routes and safe result mapping to `src/GaussAuth.Api/Memberships/membershipsEndpoints.extension.cs`.

**Checkpoint**: All membership lifecycle and isolation rules are independently
functional; effective eligibility is active user AND application AND
membership, without automatic parent-to-membership state cascades.

---

## Phase 6: Polish and Cross-Cutting Validation

**Purpose**: Verify the completed feature against its migration, architecture,
security, and end-to-end acceptance criteria.

- [ ] T033 Verify every new C# file has exactly one top-level type and its `<name>.<type>.cs` filename, and extend `tests/GaussAuth.Foundation.Tests/architectureTests.test.cs` only if an uncovered boundary requires an assertion.
- [ ] T034 Review `src/GaussAuth.Api/Applications/`, `src/GaussAuth.Api/Memberships/`, and `src/GaussAuth.Application/` for explicit limits, trusted-caller-only scope, structured identifier/outcome logging, and responses that omit SQL, stack traces, EF state, and unnecessary personal data.
- [ ] T035 Run the migration, application/membership curl scenarios, repeated migration check, and full Docker test suite from `specs/003-applications-memberships/quickstart.md`; correct only feature-003 artifacts and implementation failures found.

---

## Dependencies and Execution Order

- Phase 1 precedes Phase 2.
- Phase 2 blocks all user stories because it establishes the domain model,
  persistence constraints, migration, and registrations.
- US1 (Phase 3) is the MVP and enables application context.
- US2 (Phase 4) depends on US1 because membership creation requires a
  registered application; it also depends on the existing feature-002 User.
- US3 (Phase 5) depends on US2 because it transitions an existing membership.
- Phase 6 follows all desired stories.

## Parallel Opportunities

None are scheduled. The project constitution prefers one well-contextualized
sequential implementation; tasks share DbContext mapping, migration, DI, and
endpoint composition. Use the listed order rather than splitting work across
agents solely by file boundaries.

## Implementation Strategy

1. Complete the shared model/migration foundation and validate it in Docker.
2. Deliver US1 as the independently testable application-context MVP.
3. Add US2 to create/query database-enforced memberships.
4. Add US3 to enforce independent membership lifecycle and isolation.
5. Run the quickstart and complete cross-cutting security/architecture review.

## Format Validation

All 35 implementation tasks use the required checkbox, sequential `T###` id,
story labels only inside story phases, and explicit repository-relative file
paths. No `[P]` markers are used because sequential execution is required by
project governance.
