# Implementation Plan: Authorization Contract

**Branch**: `008-authorization-contract` | **Date**: 2026-10-01 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/008-authorization-contract/spec.md`

## Summary

Publish one authoritative authorization-context operation for consuming APIs. It validates the short-lived user access credential and persisted session state from feature 006, authenticates the consumer with an independent service credential bound to its application, and returns only stable identifiers, active roles, and unique current permissions.

## Technical Context

**Language/Version**: C# / .NET 10
**Primary Dependencies**: ASP.NET Core minimal APIs, ASP.NET Core Identity (Infrastructure only), EF Core, PostgreSQL provider
**Storage**: Existing PostgreSQL 17 Auth data and persisted sessions; external deployment configuration for consumer service credentials
**Testing**: MSTest integration and architecture tests in the Docker SDK container
**Target Platform**: Containerized Linux web API
**Project Type**: Web service
**Performance Goals**: Fresh authorization is prioritized; each context request performs current session and authorization lookup, with no cache.
**Constraints**: User bearer credential and consumer service credential are separate header secrets and never logged; roles and permissions remain dynamic and application-scoped.
**Scale/Scope**: One stable context contract, one service-credential validation boundary, dynamic authorization resolution, documentation, and essential integration coverage.

## Constitution Check

| Gate | Result | Evidence |
|---|---|---|
| Bounded Auth service | PASS | Generic identity/capabilities only; business APIs retain business rules. |
| Hexagonal dependencies | PASS | Application ports, Infrastructure validation, API transport DTOs. |
| Application isolation | PASS | Consumer credential, user session, roles, and permissions bind to one application. |
| Security by design | PASS | Separate secrets, safe failures, no cache weakening revocation/freshness. |
| Simplicity | PASS | Reuses session and authorization boundaries; no OAuth/OIDC, mTLS, cache, or dependency. |
| Testing/error contracts | PASS | Integration coverage includes context, mismatch, revocation, freshness, secret isolation, and consumer-equivalent permission check. |

## Project Structure

### Documentation (this feature)

```text
specs/008-authorization-contract/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
└── contracts/authorization-context-api.md
```

### Source Code (repository root)

```text
src/
├── GaussAuth.Application/AuthorizationContext/
│   ├── authorizationContextService.service.cs
│   └── Ports/consumerCredentialValidator.interface.cs
├── GaussAuth.Infrastructure/AuthorizationContext/
│   └── configuredConsumerCredentialValidator.service.cs
└── GaussAuth.Api/AuthorizationContext/
    ├── authorizationContextEndpoints.extension.cs
    └── authorizationContextResponse.dto.cs

tests/GaussAuth.Foundation.Tests/
├── authorizationContractTests.test.cs
└── architectureTests.test.cs
```

**Structure Decision**: Add a vertical `AuthorizationContext` slice. Extend existing session and authorization repositories only through focused Application ports.

## Design Decisions

1. `AuthorizationContextService` first validates the service credential for the declared application, then invokes the existing authoritative session validation with the user credential and application code.
2. A focused consumer-credential port reads external configuration containing distinct current and optional retiring values per application. The Infrastructure adapter performs a platform constant-time comparison, rejects reuse across applications at startup, and never returns a secret.
3. Focused current-role and effective-permission queries return only active application-scoped data. Permissions are deduplicated and roles contain only stable ID/name data.
4. `POST /auth/authorization-context` receives bearer user credential, declared application code, and consumer service secret. Any invalid consumer or user-access condition produces the established safe unauthorized response.
5. Rotation is configuration-only: accept a current and temporary retiring value for the same application, switch consumers, then remove the retiring value. No public contract, user credential, or application identity changes.

## Post-Design Constitution Check

All gates remain PASS. The design does not expose persistence/Identity models, add custom cryptography, or introduce direct consumer database coupling.

## Complexity Tracking

No constitutional violation or complexity exception is required.
