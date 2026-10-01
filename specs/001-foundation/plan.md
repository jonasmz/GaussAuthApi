# Implementation Plan: Executable Service Foundation

**Branch**: `001-foundation` | **Date**: 2026-10-01 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/001-foundation/spec.md`

## Summary

Create a .NET 10 solution with Domain, Application, Infrastructure, and API projects.
Keep Identity and EF Core in Infrastructure, persist only Identity's user-side foundation
in PostgreSQL 17, and validate the initial migration. Provide a two-service local
Docker Compose workflow for PostgreSQL and a purpose-specific .NET 10 SDK container.
The SDK service runs build/migration/test commands on demand and runs the API when
started as a service; it is not a third application container. Use the roleless
Identity schema Version2 so this foundation does not preselect passkeys or global roles.
Expose one safe liveness endpoint and use built-in ASP.NET Core error/logging facilities.
No authentication, authorization, user-management, or production deployment feature
is implemented.

## Technical Context

**Language/Version**: C# on .NET 10; nullable reference types enabled.

**Primary Dependencies**: ASP.NET Core Web API and Identity; EF Core 10;
Npgsql.EntityFrameworkCore.PostgreSQL 10; Microsoft.EntityFrameworkCore.Design and
a version-pinned local dotnet-ef tool for migrations. Microsoft test tooling,
MSTest, and Microsoft.AspNetCore.Mvc.Testing only for essential validation.
Exact compatible patch versions are pinned during implementation.

**Storage**: PostgreSQL 17 development service; EF Core migrations in Infrastructure.
No business-domain tables in this feature.

**Testing**: `dotnet test` for architecture and API composition/error checks;
a PostgreSQL-backed migration smoke check against the development service.
No coverage target or broad synthetic suite.

**Target Platform**: Linux development containers controlled by the host Docker
daemon through its mounted socket. Production platform remains undecided.

**Project Type**: ASP.NET Core Web API plus three inner-layer class libraries.

**Performance Goals**: No throughput or latency target is stated for this
foundation; build, startup, migration, and availability are the acceptance gates.

**Constraints**: No committed secrets; no host .NET/PostgreSQL prerequisite;
no third-party architecture, mapping, logging, validation, mediator, or result
framework; no functional identity endpoint; one top-level type per C# file.

**Scale/Scope**: Four production projects, one focused test project, two
development Compose services, one operational HTTP endpoint, and one initial
migration. No future feature slices are scaffolded.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Constitutional gate | Pre-research | Post-design evidence |
|---|---|---|
| Bounded service; no business functionality | PASS | Only operational route and Identity infrastructure are planned; no business tables or workflows. |
| Dependency direction and vertical slices | PASS | Domain → none; Application → Domain; Infrastructure → Application/Domain; API → Application/Infrastructure. Future slices are added only when needed. |
| Domain independent of frameworks and Identity | PASS | Domain and Application have no EF, ASP.NET, Identity, PostgreSQL, Docker, API, or Infrastructure references. Identity persistence types stay in Infrastructure. |
| Multi-application roles and least privilege | PASS | No role or permission feature is created; roleless Identity storage avoids a global-role model. |
| Fixed stack and migration governance | PASS | .NET 10, ASP.NET Core Identity, EF Core 10 with Npgsql, PostgreSQL 17; initial migration and snapshot tracked. |
| Container development rule | PASS | Existing Codex container uses host Docker socket; separate PostgreSQL and .NET SDK containers. |
| Security, configuration, errors, and logging | PASS | Ignored development credentials, validated connection settings, built-in structured logging and safe Problem Details; no sensitive endpoint. |
| Simplicity and dependency governance | PASS | Compose coordinates only required services; each added package supports a named foundation gate. No generic ports or speculative implementation. |
| Essential tests and C# file convention | PASS | Focused architecture, startup/error, and migration checks; nullable enabled and filename/type rule reviewed. |
| Spec-Kit workflow and agent rules | PASS | Plan follows the feature spec; one sequential agent; tasks will follow this plan. Open constitutional decisions remain open. |

**Gate result**: PASS before research and after design. No constitutional exception
or complexity waiver is needed.

## Project Structure

### Documentation (this feature)

```text
specs/001-foundation/
├── spec.md
├── checklists/requirements.md
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/operational-api.md
└── tasks.md                 # Created later by $speckit-tasks
```

### Source Code (repository root)

```text
GaussAuth.slnx
compose.dev.yml
.env.example                # Placeholders only; .env is ignored
.config/dotnet-tools.json   # Local dotnet-ef tool manifest
src/
├── GaussAuth.Domain/
│   └── GaussAuth.Domain.csproj
├── GaussAuth.Application/
│   └── GaussAuth.Application.csproj
├── GaussAuth.Infrastructure/
│   ├── GaussAuth.Infrastructure.csproj
│   ├── Persistence/
│   │   ├── authenticationDbContext.context.cs
│   │   └── Migrations/     # One initial migration + snapshot
│   └── DependencyInjection/
│       └── infrastructureServiceCollectionExtensions.extension.cs
└── GaussAuth.Api/
    ├── GaussAuth.Api.csproj
    ├── Program.cs          # Composition statements; no declared top-level type
    └── DependencyInjection/
        ├── applicationServiceCollectionExtensions.extension.cs
        └── apiServiceCollectionExtensions.extension.cs
tests/
└── GaussAuth.Foundation.Tests/
    ├── GaussAuth.Foundation.Tests.csproj
    ├── architectureTests.test.cs
    └── startupTests.test.cs
```

**Structure Decision**: Separate projects make the reference direction inspectable
with ordinary project metadata. Domain and Application may initially contain no
feature types; the constitution's conceptual domain remains the roadmap for later
features, and no placeholder model or generic port is needed. The API owns the
composition root and thin grouped registration methods. Infrastructure owns the
Identity context and EF provider. `Program.cs` is the mandated composition-root
entry point and declares no top-level type; other C# files follow
`<name>.<type>.cs` with one type each. Generated EF migration files require
names that retain migration identity and are reviewed for the one-type rule;
rename generated migration, designer, and snapshot files to the constitutional
suffix form without changing their generated class names or migration IDs.

## Complexity Tracking

No constitutional violations require justification.
