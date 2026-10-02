# Implementation Plan: Administrative Operations

**Branch**: `011-admin-operations` | **Date**: 2026-10-01 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/011-admin-operations/spec.md`

## Summary

Put every management operation behind one permission-based administrative boundary served under `/admin/...`, reusing the existing 002–010 services instead of re-implementing them. A small Application-layer authorizer resolves the caller's session and decides between the externally configured **global administrator** list (generalizing the 009 global audit setting) and application-scoped **`auth.*` permissions** that are seeded into every Application. An ambient per-request actor context lets the single security-event recorder stamp a distinct `ActorUserId` on every administrative event, and existing services now identify the affected role, permission, membership, assignment, or session as the event subject and record events only on real state changes. New slices add user listing, an effective-authorization view, administrative session listing/revocation with bounded bulk scopes, and database-backed consumer-secret management with one-way hashes, the existing active+retiring policy, and an atomic secret-change-plus-audit transaction. The legacy unauthenticated management routes are removed so no unprotected back door remains.

## Technical Context

**Language/Version**: C# / .NET 10

**Primary Dependencies**: ASP.NET Core minimal APIs, EF Core, PostgreSQL 17, ASP.NET Core Identity `PasswordHasher` (already used for consumer hashes). No new packages.

**Storage**: PostgreSQL. Four migrations, applied in this order: `addSecurityEventActor`, `seedAdministrativePermissions`, `addApplicationConsumerCredentials`, `moveAuditPermissionToAuthNamespace`.

**Testing**: MSTest integration, migration, and architecture tests in the Docker SDK container, extending `tests/GaussAuth.Foundation.Tests`.

**Target Platform**: Containerized Linux Web API

**Project Type**: Web service

**Performance Goals**: Administrative traffic is low-volume; authorization adds one session check and one effective-permission query per request. Lists use existing keyset-style pagination (default 50, max 100).

**Constraints**: Bulk session revocation capped per request (default 1000 sessions, configurable); administrative rate limit configurable; no secrets, hashes, or tokens in responses, logs, or events.

**Scale/Scope**: Small trusted operator set; one active+one retiring consumer secret per Application.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Gate | Result | Evidence |
|---|---|---|
| I Bounded Auth service | PASS | Only identity/authorization administration; no business-domain concepts. |
| II Hexagonal + vertical slices | PASS | Authorizer and feature handlers in Application; endpoints are inbound adapters; credential store, hashing, config policy are Infrastructure behind ports; no monolithic `AdminService`. |
| III Identity and isolation | PASS | Every application-scoped operation binds the target Application to the caller's session Application (or global capability); cross-application ids are never trusted. |
| IV Roles/permissions/sessions | PASS | Authority is `auth.*` permissions resolved through existing effective-permission semantics; admin revocation reuses session revocation. |
| V Technology and persistence | PASS | EF Core migrations only; `Domain` stays free of framework types; no foreign keys into other systems. |
| VI Security by design | PASS | Bounded inputs, reserved prefix, one-time secret display, hashes only, generic cross-scope denial, startup fail-fast on legacy audit key. |
| VII Simplicity | PASS | Reuses services and one recorder; ambient actor context instead of changing every signature; no mediator, CQRS, workflow, or new packages. |
| VIII Testing and error contracts | PASS | Essential security/isolation/audit tests only; existing ProblemDetails error shape reused. |
| IX Spec-driven | PASS | Decisions in [research.md](research.md); no requirement weakened. |
| Undecided items | PASS | No token, MFA, OAuth, or deployment choice is made here. |

## Project Structure

### Documentation (this feature)

```text
specs/011-admin-operations/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── admin-api.md     # published administrative contract (kept in sync at the end)
└── tasks.md             # created by /speckit-tasks
```

### Source Code (repository root)

```text
src/
├── GaussAuth.Domain/
│   ├── Applications/consumerCredential.entity.cs        # new: current/retiring hashes + rotation invariants
│   └── Security/securityEvent.entity.cs                 # + ActorUserId
├── GaussAuth.Application/
│   ├── Administration/
│   │   ├── Authorization/                               # authorizer, scope, result, permission catalog, actor context
│   │   ├── Ports/                                       # IGlobalAdministratorPolicy, IAdministrativeActorContext, IConsumerCredentialStore
│   │   ├── Bootstrap/                                   # administrative permission seeding
│   │   ├── Users/ListUsers/                             # list/search users query + handler
│   │   ├── AuthorizationView/                           # effective authorization inspection
│   │   ├── Sessions/                                    # list, inspect, revoke one/user-in-app/app/user-global
│   │   └── ConsumerCredentials/                         # generate, rotate, retire previous, metadata
│   ├── Applications/applicationService.service.cs       # seeds permissions at registration, emits ApplicationRegistered
│   ├── Permissions/permissionService.service.cs         # rejects reserved prefix; protects platform permissions
│   ├── Security/                                        # catalog/enum/draft gain new events and actor; audit uses new policy+code
│   └── Sessions/Ports/sessionRepository.interface.cs    # + filtered list and bounded bulk lookups
├── GaussAuth.Infrastructure/
│   ├── Administration/                                  # configured global administrators; EF consumer credential store + hasher
│   ├── AuthorizationContext/                            # validator reads store, falls back to configured hashes
│   ├── Persistence/Migrations/                          # actor, seeding, credentials, audit-permission move
│   └── Security/                                        # recorder stamps actor; efSecurityAuditTransaction adapter
└── GaussAuth.Api/
    ├── Administration/                                  # authorization endpoint filter, session/credential/view endpoints
    ├── Users|Applications|Memberships|Roles|Permissions|Authorization/   # existing endpoint classes re-mapped under /admin group
    └── Program.cs                                       # /admin group + rate limit; legacy routes removed

tests/GaussAuth.Foundation.Tests/
├── administrationTests.test.cs                          # authorization, isolation, sessions, secrets, audit
├── administrationMigrationTests.test.cs                 # seeding idempotency, audit-permission migration
└── administrativeTestHost.fixture.cs                    # global-admin client helper reused by existing tests
```

**Structure Decision**: Reuse existing slices for behavior and add only the missing capabilities as new administrative slices under `Application/Administration`. The administrative boundary is enforced once, by an endpoint filter calling one Application authorizer, so no business rule is duplicated.

## Post-Design Constitution Check

All gates remain PASS after Phase 1. Accepted deviations and risks are listed in [research.md](research.md) (global admin needs a membership to obtain a session; legacy routes are removed, which requires updating existing tests; the 009 audit permission is renamed through a data migration).

## Complexity Tracking

No constitutional violations. One deliberate pattern to note: an ambient scoped actor context is used so the shared recorder can stamp the actor without changing every service signature; the alternative (threading an actor parameter through ~15 services) was rejected as higher-churn for the same guarantee.
