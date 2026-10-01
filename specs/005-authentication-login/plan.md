# Implementation Plan: Application-Scoped Authentication Login

**Branch**: `005-authentication-login` | **Date**: 2026-10-01 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/005-authentication-login/spec.md`

## Summary

Add a single public login endpoint that authenticates a user by email and password within one explicit application context, reusing ASP.NET Core Identity for password/lockout and the existing User/Application/ApplicationMembership domain model for the prerequisite active-state checks. The Application layer orchestrates the use case through two new minimal ports — credential verification and security-event recording — that Infrastructure implements against `SignInManager`/`UserManager` and structured logging respectively. No new persisted entity, migration, session, token, or authorization-transport mechanism is introduced; the feature produces only a safe, uniform success/failure outcome carrying the stable user and application identifiers needed by `006-sessions-access`.

## Technical Context

**Language/Version**: C# on .NET 10 with nullable reference types enabled.

**Primary Dependencies**: Existing ASP.NET Core Minimal APIs, ASP.NET Core Identity (`UserManager<IdentityUser<Guid>>`, newly also `SignInManager<IdentityUser<Guid>>`), EF Core 10, Npgsql EF Core provider 10, and built-in Data Annotations and `Microsoft.AspNetCore.RateLimiting`. No new runtime package.

**Storage**: Existing PostgreSQL 17 `AuthenticationDbContext` and existing Identity credential/lockout storage. No new migration; one new read method (`IUserRepository.GetByNormalizedEmailAsync`) against the existing `Users` table.

**Testing**: Existing MSTest and `Microsoft.AspNetCore.Mvc.Testing` integration suite, extended with login success/failure, state-boundary, lockout, rate-limiting, and Identity-independence tests.

**Target Platform**: Linux .NET 10 SDK container and the existing PostgreSQL 17 container from `compose.dev.yml`.

**Project Type**: ASP.NET Core Web API with Domain, Application, Infrastructure, and API projects.

**Performance Goals**: No throughput target is required. Each login request performs a small, bounded number of reads (user by normalized email, application by code, membership by user/application pair) plus one Identity password check; no broad scans or reporting queries.

**Constraints**: Login requires application code (not raw identifier), email, and password; email is normalized using the existing `002-users-profiles` semantics; every credential/account-state rejection (unknown email, wrong password, inactive user, invalid/inactive application, missing/inactive membership, account lockout) returns one uniform `401` response; rate-limit rejections use their own `429` response ahead of the use case, per the 2026-10-01 clarification. Lockout and rate-limit thresholds are configuration-driven, defaulting to Identity's own built-in lockout defaults and the existing `user-creation` rate-limit defaults respectively.

**Scale/Scope**: One new vertical slice (Login), two new minimal Application ports, one new Infrastructure read method and two new Infrastructure adapters, one new API route, and proportionate high-value tests. No session, token, MFA, OAuth/OIDC, password recovery, or email-confirmation capability.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Constitutional gate | Pre-research | Post-design evidence |
|---|---|---|
| Bounded authentication service | PASS | Adds only identity/authentication behavior already within the service's charter; no consuming-business data. |
| Hexagonal architecture and vertical slices | PASS | New `Login` Application slice; Domain untouched; Infrastructure implements two new focused ports; API remains an inbound adapter translating to the use case. |
| Domain identity and application isolation | PASS | Reuses existing User/Application/ApplicationMembership entities and their active-state/isolation rules without modification; membership state stays strictly per-application. |
| Roles, permissions, and sessions | PASS | Authentication stays separate from authorization; no roles/permissions embedded in the login result; no session/token mechanism introduced. |
| Fixed technology and persistence | PASS | Reuses .NET 10, EF Core/Npgsql, PostgreSQL 17, and ASP.NET Core Identity; no new migration; one additive repository read method. |
| Container development rule | PASS | Reuses the existing `postgres` and `sdk` compose services; all .NET work remains in SDK Docker containers. |
| Security by design | PASS | Passwords never leave Infrastructure; uniform safe failure contract; rate limiting + Identity lockout both preserved as distinct layers; unexpected failures handled by the existing `SafeExceptionHandler`. |
| Simplicity and dependency governance | PASS | No new package; reuses existing rate-limiter, DI, and Problem Details infrastructure; two narrowly-scoped new ports, no generic identity/mediator abstraction. |
| Proportionate testing and error contracts | PASS | Tests cover login success/failure, inactive user/application, invalid membership, lockout, and critical rate limiting as the constitution requires; errors map through existing Problem Details conventions. |
| Specification-driven workflow | PASS | Plan derives from the clarified `005-authentication-login` specification and prior completed features. |

**Gate result**: PASS before research and after Phase 1 design. No exception or complexity waiver is required.

## Project Structure

### Documentation (this feature)

```text
specs/005-authentication-login/
├── spec.md
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── login-api.md
└── tasks.md                         # Created later by $speckit-tasks
```

### Source Code (repository root)

```text
src/
├── GaussAuth.Application/
│   ├── Login/
│   │   ├── Ports/
│   │   │   └── credentialVerificationService.interface.cs
│   │   ├── loginService.service.cs
│   │   └── loginOperationResult.result.cs
│   ├── Security/
│   │   ├── Ports/
│   │   │   └── securityEventRecorder.interface.cs
│   │   └── securityEventType.enum.cs
│   └── Users/Ports/
│       └── userRepository.interface.cs      # +GetByNormalizedEmailAsync
├── GaussAuth.Infrastructure/
│   ├── Identity/
│   │   └── identityCredentialVerificationService.service.cs
│   ├── Security/
│   │   └── loggingSecurityEventRecorder.service.cs
│   ├── Persistence/
│   │   └── userRepository.repository.cs      # +GetByNormalizedEmailAsync
│   └── DependencyInjection/
│       └── infrastructureServiceCollectionExtensions.extension.cs  # +SignInManager, +Lockout options, +new ports
├── GaussAuth.Api/
│   ├── Login/
│   │   ├── loginRequest.dto.cs
│   │   ├── loginResponse.dto.cs
│   │   └── loginEndpoints.extension.cs
│   └── DependencyInjection/
│       └── apiServiceCollectionExtensions.extension.cs  # +"login" rate-limiting policy
└── tests/GaussAuth.Foundation.Tests/
    └── authenticationLoginTests.test.cs
```

**Structure Decision**: Extend the existing Domain → Application → Infrastructure → API layout with one new `Login` vertical slice (named per the constitution's own slice vocabulary) plus a small cross-cutting `Security` slice for the minimal security-event port, matching the established service/result/port pattern from `Roles`/`Permissions`/`Memberships` rather than introducing a mediator, CQRS handler pipeline, or generic identity abstraction. The Domain project gains no new files; no migration is added.

## Complexity Tracking

No constitutional violations require tracking.
