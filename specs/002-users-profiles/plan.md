# Implementation Plan: Global User Identity and Profile

**Branch**: `002-users-profiles` | **Date**: 2026-10-01 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/002-users-profiles/spec.md`

## Summary

Introduce a Domain `User` aggregate (identity + state) with an owned
`UserProfile`, independent of ASP.NET Core Identity types. Infrastructure
reconciles the Domain user with an `IdentityUser<Guid>` credential row via a
shared primary key, so the same `Guid` identifies the domain aggregate and its
Identity credential without duplicating identity generation. Application
exposes five use cases (create, retrieve, update profile, activate,
deactivate) behind two small ports (`IUserRepository`,
`ICredentialProvisioningService`) that Infrastructure implements against the
existing `AuthenticationDbContext` and `UserManager<IdentityUser<Guid>>`. The
API adds five Minimal API routes under `/users`, reusing the Problem Details
and safe-exception-handling foundation from `001-foundation`. User creation
runs Identity credential provisioning and domain/profile persistence inside
one database transaction so no partial state survives a mid-operation
failure. No caller authentication, rate limiting, or email-change capability
is added, per the feature's clarified scope.

## Technical Context

**Language/Version**: C# on .NET 10; nullable reference types enabled
(unchanged from `001-foundation`).

**Primary Dependencies**: ASP.NET Core Web API (Minimal APIs), ASP.NET Core
Identity EF store, EF Core 10, Npgsql.EntityFrameworkCore.PostgreSQL 10 — all
already pinned by `001-foundation`. `Microsoft.Extensions.Identity.Core`'s
`ILookupNormalizer` is reused for email normalization. No new third-party
package is introduced; validation uses built-in
`System.ComponentModel.DataAnnotations` plus Minimal API's native
`TypedResults.ValidationProblem`.

**Storage**: PostgreSQL 17 via the existing `AuthenticationDbContext`,
extended with two new tables (`Users`, `UserProfiles`) related to the
existing `AspNetUsers` table by a shared primary key. One new EF Core
migration adds this schema; no new migrations project.

**Testing**: Extend the existing `tests/GaussAuth.Foundation.Tests` project
(MSTest + `Microsoft.AspNetCore.Mvc.Testing`) with feature-specific test
files; no second test project is created.

**Target Platform**: Same Linux development containers and
`compose.dev.yml` services (`postgres`, `sdk`) established by
`001-foundation`; unchanged.

**Project Type**: ASP.NET Core Web API plus three inner-layer class
libraries (unchanged structure; only content is added).

**Performance Goals**: No throughput/latency target is defined by the spec;
unchanged from the foundation (no streaming, pagination, or bulk operation
is in scope).

**Constraints**: No new runtime dependency; no custom password hashing; no
caller authentication/authorization in this feature (FR-023); login email
immutable via profile update (FR-013); last-write-wins profile-update
concurrency (clarified); idempotent activate/deactivate (clarified); every
request DTO enforces an explicit field length limit (constitution Security
constraint).

**Scale/Scope**: One new migration, two new Domain types, five Application
use cases, two Application ports, two Infrastructure adapters, five API
routes, and additive test files. No application/membership/role/permission/
session table or endpoint.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Constitutional gate | Pre-research | Post-design evidence |
|---|---|---|
| Bounded service; no business functionality | PASS | Only `User`/`UserProfile` are added; no business entity, no application/membership/role/permission/session table or endpoint. |
| Dependency direction and vertical slices | PASS | Domain → none; Application → Domain only, behind `IUserRepository`/`ICredentialProvisioningService` ports; Infrastructure implements both ports; API → Application/Infrastructure. Slices named `Users` and `Profiles` per the constitution's example list. |
| Domain independent of frameworks and Identity | PASS | `User`/`UserProfile` are plain classes with no EF Core, ASP.NET Core, or Identity reference; Identity integration (`UserManager`, `ILookupNormalizer`) lives only in Infrastructure's `ICredentialProvisioningService` implementation. |
| Multi-application roles and least privilege | N/A (deferred) | No role, permission, application, or membership concept is touched by this feature; left to `003-applications-memberships` as scoped by the constitution. |
| Fixed stack and migration governance | PASS | .NET 10, EF Core 10/Npgsql, PostgreSQL 17, ASP.NET Core Identity — all already pinned; one version-controlled migration adds only `Users`/`UserProfiles` schema. |
| Container development rule | PASS | Reuses `compose.dev.yml`'s `postgres`/`sdk` services; no new container. |
| Security, configuration, errors, and logging | PASS | Email/password handled only by Identity; Problem Details/safe exception handler extended, not replaced; structured logging excludes passwords/hashes/security stamps; explicit per-field length limits (see `data-model.md`). Caller authentication is explicitly out of scope per the spec's Clarifications session (FR-023) — these routes are not deployed as public endpoints; the real access boundary is a decision for the future authentication/authorization feature, consistent with the constitution's undecided-until-specified list. |
| Simplicity and dependency governance | PASS | No new package; two small ports (not a generic repository or mediator framework); a per-operation result type replaces exceptions for expected failures without introducing a generic result framework; existing test project is reused instead of adding a second one. |
| Essential tests and C# file convention | PASS | Tests cover creation, duplicate-email rejection, retrieval, profile update, activation/deactivation, and migration/persistence; existing `ArchitectureTests` already enforce the one-type/filename rule and forbidden-reference checks across the unchanged project set. |
| Spec-Kit workflow and agent rules | PASS | Plan follows the clarified spec; one sequential agent; tasks will derive from this plan. |

**Gate result**: PASS before research and after design. No constitutional
exception or complexity waiver is needed.

## Project Structure

### Documentation (this feature)

```text
specs/002-users-profiles/
├── spec.md
├── checklists/requirements.md
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/users-api.md
└── tasks.md                 # Created later by $speckit-tasks
```

### Source Code (repository root)

```text
src/
├── GaussAuth.Domain/
│   └── Users/
│       ├── user.entity.cs            # Aggregate root: identity, state, Profile
│       └── userProfile.entity.cs     # Owned entity: reusable profile fields
├── GaussAuth.Application/
│   └── Users/
│       ├── Ports/
│       │   ├── userRepository.interface.cs
│       │   └── credentialProvisioningService.interface.cs
│       ├── CreateUser/
│       │   ├── createUser.command.cs
│       │   ├── createUser.result.cs
│       │   └── createUser.handler.cs
│       ├── GetUser/
│       │   ├── getUser.query.cs
│       │   └── getUser.handler.cs
│       ├── ActivateUser/
│       │   └── activateUser.handler.cs
│       ├── DeactivateUser/
│       │   └── deactivateUser.handler.cs
│       └── Profiles/
│           ├── updateProfile.command.cs
│           ├── updateProfile.result.cs
│           └── updateProfile.handler.cs
├── GaussAuth.Infrastructure/
│   ├── Persistence/
│   │   ├── authenticationDbContext.context.cs   # Extended: DomainUsers/UserProfiles DbSets
│   │   ├── Migrations/                          # + one new migration (Users/UserProfiles)
│   │   └── userRepository.repository.cs
│   ├── Identity/
│   │   └── identityCredentialProvisioningService.service.cs
│   └── DependencyInjection/
│       └── infrastructureServiceCollectionExtensions.extension.cs  # Extended
└── GaussAuth.Api/
    ├── Users/
    │   ├── usersEndpoints.extension.cs
    │   ├── createUserRequest.dto.cs
    │   ├── updateProfileRequest.dto.cs
    │   └── userResponse.dto.cs
    ├── DependencyInjection/
    │   └── applicationServiceCollectionExtensions.extension.cs     # Extended
    └── Program.cs                                                  # Extended: MapUsersEndpoints()

tests/
└── GaussAuth.Foundation.Tests/
    ├── createUserTests.test.cs
    ├── userRetrievalTests.test.cs
    ├── profileUpdateTests.test.cs
    ├── userActivationTests.test.cs
    └── userPersistenceTests.test.cs
```

**Structure Decision**: New Domain/Application content lives under a `Users/`
(and `Profiles/` for the update slice) folder per project, matching the
constitution's named vertical slices. No new project is created: Domain,
Application, Infrastructure, and API already exist and already enforce the
required reference direction via `ArchitectureTests`, which also already
scans every `.cs` file under `src/` and `tests/` for the one-type/filename
rule — no test or check needs updating for the new files to be covered. The
existing `AuthenticationDbContext` is extended in place (same file) rather
than duplicated, since it is the single source of Identity + domain
persistence for this bounded service. The existing
`tests/GaussAuth.Foundation.Tests` project is reused rather than creating a
second test project, consistent with the constitution's simplicity
principle; it already references all four production assemblies and the
required Microsoft test/Mvc.Testing packages.

## Complexity Tracking

No constitutional violations require justification.
