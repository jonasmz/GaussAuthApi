# Implementation Plan: Applications and Memberships

**Branch**: `003-applications-memberships` | **Date**: 2026-10-01 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/003-applications-memberships/spec.md`

**Note**: This template is filled in by the `$speckit-plan` command; its definition describes the execution workflow.

## Summary

Add explicit consuming-application context and durable global-user membership
to the existing authentication service. The implementation adds framework-free
`Application` and `ApplicationMembership` domain types, focused Application
handlers and persistence ports, EF Core mappings/migration, and Minimal API
routes. It reuses the existing `User` aggregate, `AuthenticationDbContext`,
safe Problem Details handling, Docker development workflow, and test project.
The design enforces unique normalized application codes and unique user/app
pairs in PostgreSQL, while keeping memberships independent across application
contexts. It deliberately adds no roles, permissions, login, tokens, or
sessions.

## Technical Context

**Language/Version**: C# on .NET 10 with nullable reference types enabled.

**Primary Dependencies**: Existing ASP.NET Core Minimal APIs, EF Core 10,
Npgsql EF Core provider 10, and ASP.NET Core Identity EF store. No new package
is required; validation continues to use built-in Data Annotations and native
Minimal API validation responses.

**Storage**: Existing PostgreSQL 17 `AuthenticationDbContext`. One EF Core
migration adds `Applications` and `ApplicationMemberships`, their indexes, and
foreign keys to existing domain `Users` (not Identity's `AspNetUsers`).

**Testing**: Extend `tests/GaussAuth.Foundation.Tests` (MSTest and
`Microsoft.AspNetCore.Mvc.Testing`) with essential application, membership,
migration, and isolation tests. Reuse existing architecture checks.

**Target Platform**: Linux .NET 10 SDK container and PostgreSQL 17 container
from `compose.dev.yml`; .NET commands run only in the `sdk` container.

**Project Type**: ASP.NET Core Web API with Domain, Application,
Infrastructure, and API projects.

**Performance Goals**: No throughput or latency target is required. Operations
are bounded, single-record management operations; no bulk import, reporting,
or broad search is in scope.

**Constraints**: Stable normalized application codes; immutable code and name;
explicit application context for membership read/mutation/eligibility; no
cascade membership state changes when a parent user or application is
deactivated; idempotent state changes; PostgreSQL uniqueness and referential
integrity are authoritative; safe error contracts; no new runtime dependency,
container, role, or authorization substitute; every collection listing uses
an opaque cursor and a validated `limit` of 1–100.

**Scale/Scope**: Two Domain types, focused Applications and Memberships
vertical slices, two small persistence ports/adapters, one EF migration, twelve
management/listing routes, additive DTOs, and proportionate test files.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Constitutional gate | Pre-research | Post-design evidence |
|---|---|---|
| Bounded authentication service | PASS | Adds only registered applications and global-user/application membership. No consuming-domain entity or data is modelled. |
| Dependency direction and vertical slices | PASS | Plain Domain types; Application use cases depend on targeted ports; Infrastructure implements EF adapters; API is an inbound adapter. Slices are named `Applications` and `Memberships`. |
| Framework-independent Domain and isolation | PASS | Domain has no EF, ASP.NET Core, Identity, or PostgreSQL reference. Every membership mutation/lookup route identifies an application; eligibility requires active user, application, and membership. |
| Fixed stack and migration governance | PASS | Reuses .NET 10, EF Core/Npgsql, PostgreSQL 17, Identity integration, and one version-controlled migration. |
| Container development rule | PASS | Reuses only `postgres` and `sdk` in `compose.dev.yml`; no host SDK/PostgreSQL prerequisite or duplicate database container. |
| Security, validation, errors, logging | PASS | Bounded code/name and identifier input, safe 400/404/409 Problem Details, structured identifier/outcome logging, no full payload/PII dumps. Management endpoints retain the existing trusted-caller boundary without inventing roles or login. |
| Simplicity and dependency governance | PASS | Reuses existing DbContext, test project, handlers, validation, and exception patterns. Two feature-specific ports avoid generic repositories, mediators, and result frameworks. |
| Essential tests and file convention | PASS | Tests target lifecycle, uniqueness races, foreign keys, inactive parent policy, isolation, and migration. Existing architecture tests cover one-top-level-type/file and forbidden references. |
| Spec-driven workflow | PASS | Research, data model, contracts, and quickstart derive from clarified feature 003; tasks remain a later phase. |

**Gate result**: PASS before research and after design. No constitutional
exception or complexity waiver is needed.

## Project Structure

### Documentation (this feature)

```text
specs/003-applications-memberships/
├── spec.md
├── checklists/requirements.md
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/applications-memberships-api.md
└── tasks.md                         # Created later by $speckit-tasks
```

### Source Code (repository root)
```text
src/
├── GaussAuth.Domain/
│   ├── Applications/application.entity.cs
│   └── Memberships/applicationMembership.entity.cs
├── GaussAuth.Application/
│   ├── Applications/                         # Ports + create/get/list/activate/deactivate slices
│   └── Memberships/                          # Port + create/get/list/activate/deactivate slices
├── GaussAuth.Infrastructure/
│   └── Persistence/                          # DbContext, two repositories, migration
└── GaussAuth.Api/
    ├── Applications/                         # endpoint extension and DTOs
    ├── Memberships/                          # endpoint extension and DTOs
    └── Program.cs                            # maps endpoint extensions

tests/
└── GaussAuth.Foundation.Tests/
    ├── applicationsTests.test.cs
    ├── applicationMembershipsTests.test.cs
    └── usersSchemaMigrationTests.test.cs     # updated schema expectations
```

**Structure Decision**: Extend the existing four-project hexagonal structure,
single DbContext, DI extension points, and test project. Application and
Memberships remain separate vertical slices; their small ports are explicit
because they have distinct persistence responsibilities. No new project,
generic data access layer, test project, container, or infrastructure service
is introduced.

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

No constitutional violations require justification.
