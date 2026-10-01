---

description: "Actionable implementation tasks for application-scoped roles and permissions"
---

# Tasks: Roles and Permissions

**Input**: Design documents from `specs/004-roles-permissions/`

**Prerequisites**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md), [data-model.md](data-model.md), [roles-permissions-api.md](contracts/roles-permissions-api.md), and [quickstart.md](quickstart.md)

**Tests**: Essential integration, migration, and architecture tests are included because the specification explicitly requires uniqueness, active-state, history, effective-permission, persistence, and isolation behavior. Run all .NET commands in the existing `sdk` Docker service.

**Organization**: Tasks are sequential by design. The constitution prohibits parallel execution merely because files differ; DbContext, composite foreign keys, migrations, and endpoint composition require one coherent implementation path.

## Phase 1: Setup

**Purpose**: Confirm feature-003 baseline and established conventions before additive authorization work.

- [ ] T001 Review `specs/004-roles-permissions/spec.md`, `plan.md`, `data-model.md`, `contracts/roles-permissions-api.md`, and `research.md`; record no deviation from clarified code, immutable-name, history, and isolation rules in `specs/004-roles-permissions/tasks.md`.
- [ ] T002 Inspect `src/GaussAuth.Infrastructure/Persistence/authenticationDbContext.context.cs`, `src/GaussAuth.Api/Program.cs`, `src/GaussAuth.Api/DependencyInjection/applicationServiceCollectionExtensions.extension.cs`, `src/GaussAuth.Infrastructure/DependencyInjection/infrastructureServiceCollectionExtensions.extension.cs`, and existing feature-003 repositories to preserve composition, safe-error, and migration conventions.

---

## Phase 2: Foundational Domain, Integrity, and Composition

**Purpose**: Establish the framework-independent authorization model and persistence invariants shared by every user story.

**⚠️ CRITICAL**: Complete this phase before user-story endpoints or relationship operations.

- [ ] T003 Create framework-independent `Role` with non-empty stable id/application id, immutable trimmed name and normalized name, optional description maximum 500 characters, `IsActive`, UTC timestamps, and idempotent activation/deactivation in `src/GaussAuth.Domain/Roles/role.entity.cs`.
- [ ] T004 Create framework-independent `Permission` with non-empty stable id/application id, immutable canonical code, optional description maximum 500 characters, `IsActive`, UTC timestamps, and idempotent activation/deactivation in `src/GaussAuth.Domain/Permissions/permission.entity.cs`.
- [ ] T005 Create framework-independent `RolePermission` and `UserRole` with stable non-empty ids, explicit `ApplicationId`, required parent ids, `IsActive`, UTC timestamps, and idempotent activation/deactivation in `src/GaussAuth.Domain/Authorization/rolePermission.entity.cs` and `src/GaussAuth.Domain/Authorization/userRole.entity.cs`.
- [ ] T006 Define targeted role and permission ports for context reads, cursor lists, add/save, and duplicate-safe persistence outcomes in `src/GaussAuth.Application/Roles/Ports/roleRepository.interface.cs` and `src/GaussAuth.Application/Permissions/Ports/permissionRepository.interface.cs`.
- [ ] T007 Define targeted authorization ports for RolePermission/UserRole reads, lists, add/save, and one effective-permission query in `src/GaussAuth.Application/Authorization/Ports/rolePermissionRepository.interface.cs` and `src/GaussAuth.Application/Authorization/Ports/userRoleRepository.interface.cs`.
- [ ] T008 Extend `src/GaussAuth.Infrastructure/Persistence/authenticationDbContext.context.cs` with Roles, Permissions, RolePermissions, and UserRoles mappings: Role `(ApplicationId, NormalizedName)` unique; Permission `(ApplicationId, Code)` unique; RolePermission `(RoleId, PermissionId)` unique plus `(RoleId, ApplicationId)` and `(PermissionId, ApplicationId)` composite restrictive foreign keys; UserRole `(UserId, RoleId, ApplicationId)` unique plus composite restrictive foreign keys to ApplicationMembership and Role; add supporting alternate keys/indexes needed by composite foreign keys without EF attributes in Domain types.
- [ ] T009 Implement EF persistence adapters and translate only the named PostgreSQL uniqueness constraints to expected duplicate results in `src/GaussAuth.Infrastructure/Persistence/roleRepository.repository.cs`, `permissionRepository.repository.cs`, `rolePermissionRepository.repository.cs`, and `userRoleRepository.repository.cs`.
- [ ] T010 Register new authorization persistence ports/adapters with existing scoped lifetimes in `src/GaussAuth.Infrastructure/DependencyInjection/infrastructureServiceCollectionExtensions.extension.cs`.
- [ ] T011 Generate one EF Core migration and update the model snapshot under `src/GaussAuth.Infrastructure/Persistence/Migrations/` for exactly Roles, Permissions, RolePermissions, UserRoles, named uniqueness constraints, composite foreign keys, and restrictive delete behavior; generate only inside the existing `sdk` Docker container.
- [ ] T012 Update migration/schema expectations for all four tables, unique constraints, composite foreign keys, and repeated migration application in `tests/GaussAuth.Foundation.Tests/usersSchemaMigrationTests.test.cs` and `tests/GaussAuth.Foundation.Tests/migrationTests.test.cs`.

**Checkpoint**: Domain is framework-independent, PostgreSQL rejects duplicate and cross-application rows, and the migration applies cleanly.

---

## Phase 3: User Story 1 - Define Authorization Within an Application (Priority: P1) 🎯 MVP

**Goal**: Create, query, list, and transition application-scoped roles and permissions without modeling consuming-business data.

**Independent Test**: Create valid roles and permissions for two active applications; verify same normalized role name/code works across applications, fails in the same application, and lifecycle transitions remain idempotent.

### Tests for User Story 1

- [ ] T013 [US1] Add integration tests for role creation, trim/case normalization, required name, name length 1–200, description maximum 500, same-application duplicate `409`, cross-application same-name `201`, inactive/nonexistent application failures, and concurrent uniqueness in `tests/GaussAuth.Foundation.Tests/rolesPermissionsTests.test.cs`.
- [ ] T014 [US1] Add integration tests for permission creation, required canonical lowercase code length 3–128 matching `^[a-z0-9]+(?:-[a-z0-9]+)*(?:\.[a-z0-9]+(?:-[a-z0-9]+)*)*$`, description maximum 500, same-application duplicate `409`, cross-application same-code `201`, inactive/nonexistent application failures, and concurrent uniqueness in `tests/GaussAuth.Foundation.Tests/rolesPermissionsTests.test.cs`.
- [ ] T015 [US1] Add integration tests for application-context role/permission retrieval, stable ascending-id cursor listing with default 50 and valid 1–100 limits, invalid pagination `400`, safe mismatched/missing `404`, activate/deactivate idempotency, and inactive-application activation conflict in `tests/GaussAuth.Foundation.Tests/rolesPermissionsTests.test.cs`.

### Implementation for User Story 1

- [ ] T016 [US1] Implement `RoleService` create/get/list/activate/deactivate operations in `src/GaussAuth.Application/Roles/roleService.service.cs`; normalize display/normalized names, require active application for create/activate, preserve immutable name, and log role id/outcome only.
- [ ] T017 [US1] Implement `PermissionService` create/get/list/activate/deactivate operations in `src/GaussAuth.Application/Permissions/permissionService.service.cs`; normalize/validate immutable codes, require active application for create/activate, and log permission id/outcome only.
- [ ] T018 [US1] Add focused role/permission operation and page result types in `src/GaussAuth.Application/Roles/roleOperationResult.result.cs`, `rolePage.result.cs`, `src/GaussAuth.Application/Permissions/permissionOperationResult.result.cs`, and `permissionPage.result.cs`.
- [ ] T019 [US1] Create bounded request/response/page DTOs with safe domain mapping in `src/GaussAuth.Api/Roles/createRoleRequest.dto.cs`, `roleResponse.dto.cs`, `roleListResponse.dto.cs`, `src/GaussAuth.Api/Permissions/createPermissionRequest.dto.cs`, `permissionResponse.dto.cs`, and `permissionListResponse.dto.cs`.
- [ ] T020 [US1] Implement the role routes from `contracts/roles-permissions-api.md` with explicit application context, 400/404/409 Problem Details, and no caller authorization model in `src/GaussAuth.Api/Roles/rolesEndpoints.extension.cs`.
- [ ] T021 [US1] Implement the permission routes from `contracts/roles-permissions-api.md` with explicit application context, 400/404/409 Problem Details, and no caller authorization model in `src/GaussAuth.Api/Permissions/permissionsEndpoints.extension.cs`.
- [ ] T022 [US1] Register `RoleService` and `PermissionService` in `src/GaussAuth.Api/DependencyInjection/applicationServiceCollectionExtensions.extension.cs` and map `MapRolesEndpoints()`/`MapPermissionsEndpoints()` in `src/GaussAuth.Api/Program.cs` without adding login, sessions, tokens, roles from Identity, or runtime authorization middleware.

**Checkpoint**: An application has an independently testable authorization vocabulary with normalized application-scoped uniqueness and lifecycle.

---

## Phase 4: User Story 2 - Compose Roles and Assign Them to Members (Priority: P1)

**Goal**: Associate active same-application permissions to active roles and active roles to eligible members, preserving historical relationships.

**Independent Test**: In one active application, assign/reassign/remove a permission to a role and a role to an active member; prove duplicate, inactive, missing-membership, and cross-application attempts are safely rejected.

### Tests for User Story 2

- [ ] T023 [US2] Add integration tests for RolePermission creation, duplicate active assignment `409`, remove/reactivate same historical id, role permission lists with cursor/limit validation, and rejection for missing, inactive role/permission/application in `tests/GaussAuth.Foundation.Tests/rolesPermissionsTests.test.cs`.
- [ ] T024 [US2] Add integration tests that RolePermission rejects permission/role pairs from different applications and remains isolated when the same permission code exists in another application in `tests/GaussAuth.Foundation.Tests/rolesPermissionsTests.test.cs`.
- [ ] T025 [US2] Add integration tests for UserRole creation, duplicate active assignment `409`, remove/reactivate same historical id, explicit application user-role lists, and rejection for missing/inactive user, application, membership, or role in `tests/GaussAuth.Foundation.Tests/rolesPermissionsTests.test.cs`.
- [ ] T026 [US2] Add integration tests that UserRole rejects role/application mismatch and absent/inactive membership, and that assignments in one application do not alter a user's assignments in another application in `tests/GaussAuth.Foundation.Tests/rolesPermissionsTests.test.cs`.

### Implementation for User Story 2

- [ ] T027 [US2] Implement RolePermission assign/remove/list operations in `src/GaussAuth.Application/Authorization/rolePermissionService.service.cs`; require active application/role/permission with matching application ids, deactivate rather than delete, reactivate a valid historical relation, and log relationship id/outcome only.
- [ ] T028 [US2] Implement UserRole assign/remove/list operations in `src/GaussAuth.Application/Authorization/userRoleService.service.cs`; require active user/application/membership/role in matching context, deactivate rather than delete, reactivate a valid historical relation, and log relationship id/outcome only.
- [ ] T029 [US2] Add authorization operation/page result types that distinguish not-found, inactive parent, cross-application mismatch, duplicate active relationship, and invalid input in `src/GaussAuth.Application/Authorization/authorizationOperationResult.result.cs` and `authorizationPage.result.cs`.
- [ ] T030 [US2] Create safe relationship/page DTOs exposing only identifiers, state, and timestamps in `src/GaussAuth.Api/Authorization/rolePermissionResponse.dto.cs`, `userRoleResponse.dto.cs`, `rolePermissionListResponse.dto.cs`, and `userRoleListResponse.dto.cs`.
- [ ] T031 [US2] Implement RolePermission assignment, removal, and role permission-list routes from `contracts/roles-permissions-api.md` in `src/GaussAuth.Api/Authorization/authorizationEndpoints.extension.cs`.
- [ ] T032 [US2] Implement UserRole assignment, removal, and explicit application/user role-list routes with safe 400/404/409 mapping in `src/GaussAuth.Api/Authorization/authorizationEndpoints.extension.cs`.
- [ ] T033 [US2] Register authorization relationship services in `src/GaussAuth.Api/DependencyInjection/applicationServiceCollectionExtensions.extension.cs`, map `MapAuthorizationEndpoints()` in `src/GaussAuth.Api/Program.cs`, and preserve trusted-caller-only scope without introducing a final authorization substitute.

**Checkpoint**: Authorization relationships are durable, reactivate rather than duplicate, and cannot cross application contexts.

---

## Phase 5: User Story 3 - Resolve Effective Permissions in Explicit Context (Priority: P1)

**Goal**: Return the unique effective permission set for one user/application only when the entire active authorization path is eligible.

**Independent Test**: Give an active member two application roles with an overlapping permission, resolve the application permission set once, then toggle every parent/relationship state and verify exclusion without leakage to another application.

### Tests for User Story 3

- [ ] T034 [US3] Add integration tests for effective permissions from active user/application/membership/UserRole/Role/RolePermission/Permission paths, duplicate elimination across multiple roles, canonical ordering, and empty result for a user with no qualifying assignment in `tests/GaussAuth.Foundation.Tests/effectivePermissionsTests.test.cs`.
- [ ] T035 [US3] Add integration tests proving inactive user, application, membership, UserRole, role, RolePermission, and permission each independently exclude affected effective permissions while retaining historical records in `tests/GaussAuth.Foundation.Tests/effectivePermissionsTests.test.cs`.
- [ ] T036 [US3] Add integration tests proving effective permissions queried for one application never include permissions or role grants from another application, including after membership reactivation in `tests/GaussAuth.Foundation.Tests/effectivePermissionsTests.test.cs`.

### Implementation for User Story 3

- [ ] T037 [US3] Implement the single targeted active-state-filtered, application-scoped effective permission repository read with distinct permission identities/codes and ascending code order in `src/GaussAuth.Infrastructure/Persistence/userRoleRepository.repository.cs`.
- [ ] T038 [US3] Implement effective-permission query handling in `src/GaussAuth.Application/Authorization/effectivePermissionService.service.cs`; return a safe not-found result for absent user/application and an empty set for inactive eligibility without modifying assignments.
- [ ] T039 [US3] Create minimal effective-permission response DTOs exposing only permission id, code, and description in `src/GaussAuth.Api/Authorization/effectivePermissionResponse.dto.cs` and `effectivePermissionListResponse.dto.cs`.
- [ ] T040 [US3] Implement `GET /applications/{applicationId}/users/{userId}/effective-permissions` with explicit context and safe `404` response mapping in `src/GaussAuth.Api/Authorization/authorizationEndpoints.extension.cs`.

**Checkpoint**: Effective authorization is unique, fully state-aware, and strictly application-isolated without runtime enforcement.

---

## Phase 6: Polish and Cross-Cutting Validation

**Purpose**: Validate persistence, architecture, safe contracts, and end-to-end behavior for the completed feature.

- [ ] T041 Verify every new C# source file has exactly one top-level type and a `<name>.<type>.cs` filename; extend `tests/GaussAuth.Foundation.Tests/architectureTests.test.cs` only if the existing boundary assertion does not cover a new authorization dependency.
- [ ] T042 Review `src/GaussAuth.Api/Roles/`, `src/GaussAuth.Api/Permissions/`, `src/GaussAuth.Api/Authorization/`, and `src/GaussAuth.Application/` for exact external limits, immutable identifiers, safe Problem Details, identifier/outcome-only logging, explicit application context, and absence of SQL, EF state, PII, IdentityRole reuse, login, tokens, sessions, or runtime enforcement.
- [ ] T043 Run migration generation/application twice, API curl scenarios for roles/permissions/relationships/effective permissions, and the full Docker test suite from `specs/004-roles-permissions/quickstart.md`; correct only feature-004 artifacts and failures found.

## Dependencies and Execution Order

- Phase 1 precedes Phase 2.
- Phase 2 blocks every user story because Domain types, composite integrity, adapters, DI, and migration are shared.
- US1 (Phase 3) creates the roles and permissions required by US2.
- US2 (Phase 4) creates the relationships required by US3.
- US3 (Phase 5) depends on US2 and validates the complete eligibility conjunction.
- Phase 6 follows all requested stories.

## Parallel Opportunities

None are scheduled. Although some tasks affect distinct files, shared DbContext mapping, migration, endpoint composition, and the project constitution require one sequential implementation stream.

## Implementation Strategy

1. Complete shared Domain, ports, composite foreign keys, migration, and registrations.
2. Deliver the independently demonstrable authorization vocabulary (US1).
3. Add durable, isolated role-permission and user-role composition (US2).
4. Add state-aware effective permission resolution (US3).
5. Validate architecture, migration idempotence, contract scenarios, and Docker tests.

## Format Validation

All 43 tasks use the required checkbox, sequential `T###` identifier, story labels only in user-story phases, and explicit repository-relative file paths. No `[P]` markers are used because sequential execution is required by project governance.
