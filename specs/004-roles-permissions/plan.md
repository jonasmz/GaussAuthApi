# Implementation Plan: Roles and Permissions

**Branch**: `004-roles-permissions` | **Date**: 2026-10-01 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/004-roles-permissions/spec.md`

## Summary

Add generic, application-scoped authorization configuration to the existing authentication service. The feature adds framework-independent Role, Permission, RolePermission, and UserRole domain types; focused application services and persistence ports; PostgreSQL-backed integrity constraints; management and effective-permission API routes; and essential integration tests. Every mutation and effective-permission query carries application context explicitly. The design preserves authorization history through inactive relationships and adds no login, token, session, or consuming-API enforcement behavior.

## Technical Context

**Language/Version**: C# on .NET 10 with nullable reference types enabled.

**Primary Dependencies**: Existing ASP.NET Core Minimal APIs, EF Core 10, Npgsql EF Core provider 10, ASP.NET Core Identity integration, and built-in Data Annotations. No new runtime package.

**Storage**: Existing PostgreSQL 17 `AuthenticationDbContext`. One additive EF Core migration creates Roles, Permissions, RolePermissions, and UserRoles with named unique indexes and restrictive foreign keys.

**Testing**: Existing MSTest and `Microsoft.AspNetCore.Mvc.Testing` integration suite, extended with authorization lifecycle, effective-permission, isolation, migration, and architecture-boundary tests.

**Target Platform**: Linux .NET 10 SDK container and the existing PostgreSQL 17 container from `compose.dev.yml`.

**Project Type**: ASP.NET Core Web API with Domain, Application, Infrastructure, and API projects.

**Performance Goals**: No throughput target is required. Each mutation is bounded to one relationship; effective-permission resolution is one explicit user/application read with duplicate elimination and no broad reporting.

**Constraints**: Role names are trimmed and compared case-insensitively, immutable after creation, and bounded to 1–200 characters. Permission codes are trimmed/lowercased, immutable, 3–128 characters, and match lowercase dot-separated alphanumeric segments with optional internal hyphens. Collections use cursor pagination, stable ascending identifiers, default limit 50, and limit 1–100. Relationship removal is non-destructive deactivation; a valid repeat request reactivates the same record. Effective permissions require active user, application, membership, user-role, role, role-permission, and permission.

**Scale/Scope**: Four Domain types, two vertical slices (Roles and Permissions), targeted persistence ports/adapters, one migration, focused management/query routes, and proportionate high-value tests. No role editing beyond optional description updates, no permission-code changes, and no authorization runtime enforcement.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Constitutional gate | Pre-research | Post-design evidence |
|---|---|---|
| Bounded authentication service | PASS | Adds only generic authorization concepts; no consuming-business data or runtime enforcement. |
| Dependency direction and vertical slices | PASS | Domain types remain plain; Application depends on authorization, user, membership, and application ports; Infrastructure implements EF adapters; API remains inbound. |
| Domain identity and application isolation | PASS | All relationships carry `ApplicationId`; composite persistence constraints and explicit-context use cases prevent cross-application links. |
| Fixed stack and migration governance | PASS | Reuses .NET 10, EF Core/Npgsql, PostgreSQL 17, Identity integration, and one version-controlled migration. |
| Container development rule | PASS | Reuses the existing `postgres` and `sdk` compose services; all .NET work remains in SDK Docker containers. |
| Security, validation, errors, logging | PASS | Bounded normalized inputs, safe 400/404/409 results, strict active-state checks, and identifier/outcome-only structured logs. |
| Simplicity and dependency governance | PASS | Uses existing DbContext, DI, Minimal API, Problem Details, and focused ports; adds no generic repository, mediator, or package. |
| Essential tests and file convention | PASS | Tests cover isolation, uniqueness, lifecycle, active-state filtering, historical reactivation, and effective permission de-duplication; one type per correctly named source file. |
| Spec-driven workflow | PASS | Plan derives from the clarified feature 004 specification and prior completed features. |

**Gate result**: PASS before research and after Phase 1 design. No exception or complexity waiver is required.

## Project Structure

### Documentation (this feature)

```text
specs/004-roles-permissions/
├── spec.md
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── roles-permissions-api.md
└── tasks.md                         # Created later by $speckit-tasks
```

### Source Code (repository root)

```text
src/
├── GaussAuth.Domain/
│   ├── Roles/role.entity.cs
│   ├── Permissions/permission.entity.cs
│   └── Authorization/
│       ├── rolePermission.entity.cs
│       └── userRole.entity.cs
├── GaussAuth.Application/
│   ├── Roles/                         # service, results, and repository port
│   ├── Permissions/                   # service, results, and repository port
│   └── Authorization/                 # assignment/effective-permission service and ports
├── GaussAuth.Infrastructure/
│   └── Persistence/                   # DbContext mapping, adapters, migration
├── GaussAuth.Api/
│   ├── Roles/                         # DTOs and endpoint extension
│   ├── Permissions/                   # DTOs and endpoint extension
│   └── Authorization/                 # DTOs and endpoint extension
└── tests/GaussAuth.Foundation.Tests/
    ├── rolesPermissionsTests.test.cs
    ├── effectivePermissionsTests.test.cs
    ├── usersSchemaMigrationTests.test.cs
    └── architectureTests.test.cs
```

**Structure Decision**: Extend the existing Domain → Application → Infrastructure → API layout with focused feature slices. Reuse the established service/result/repository pattern from applications and memberships rather than adding a mediator or a second persistence abstraction.

## Complexity Tracking

No constitutional violations require tracking.
