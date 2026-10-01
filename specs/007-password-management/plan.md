# Implementation Plan: Password Management

**Branch**: `007-password-management` | **Date**: 2026-10-01 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/007-password-management/spec.md`

**Note**: This template is filled in by the `$speckit-plan` command; its definition describes the execution workflow.

## Summary

Add password change, recovery, reset, protected development/test delivery, public rate limits, safe events, and revoke-all session policy through focused Application ports over existing Identity and Sessions capabilities.

## Technical Context


**Language/Version**: C# / .NET 10

**Primary Dependencies**: ASP.NET Core Identity, EF Core, Minimal APIs, existing rate limiter

**Storage**: Existing PostgreSQL Identity tables and `Sessions`; protected local file only for Development/Test delivery

**Testing**: MSTest integration tests in the SDK container

**Target Platform**: Linux Docker API service

**Project Type**: ASP.NET Core web service

**Performance Goals**: No throughput target; public operations remain rate limited and complete within normal API request timeouts.

**Constraints**: No custom crypto or password persistence; secrets never log; all sessions revoke after successful credential change.

**Scale/Scope**: Three API operations, protected development/test delivery, and no production email-provider selection.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

All gates PASS: password management is bounded-service scope; Domain stays infrastructure-free; Identity and file delivery remain Infrastructure; all sessions revoke through the existing model; no custom cryptography, provider, queue, or new persistence is added.

## Project Structure

### Documentation (this feature)

```text
specs/007-password-management/
├── plan.md              # This file ($speckit-plan command output)
├── research.md          # Phase 0 output ($speckit-plan command)
├── data-model.md        # Phase 1 output ($speckit-plan command)
├── quickstart.md        # Phase 1 output ($speckit-plan command)
├── contracts/           # Phase 1 output ($speckit-plan command)
└── tasks.md             # Phase 2 output ($speckit-tasks command - NOT created by $speckit-plan)
```

### Source Code (repository root)

```text
src/GaussAuth.Application/{Passwords,Sessions}/
src/GaussAuth.Infrastructure/{Identity,Passwords}/
src/GaussAuth.Api/Passwords/
tests/GaussAuth.Foundation.Tests/passwordManagementTests.test.cs
```

**Structure Decision**: Existing vertical slices are extended without new projects.

**Concrete structure**: `GaussAuth.Application/Passwords` owns `PasswordManagementService` and focused ports; `GaussAuth.Application/Sessions` exposes a per-user revoke-all use case; `GaussAuth.Infrastructure/Identity` implements Identity credential operations; `GaussAuth.Infrastructure/Passwords` implements protected delivery; `GaussAuth.Api/Passwords` maps the three routes; `tests/GaussAuth.Foundation.Tests/passwordManagementTests.test.cs` covers all flows.

**Session boundary**: expose `RevokeAllForUserAsync(Guid userId, CancellationToken)` from the Sessions Application service/port. It loads matching sessions, revokes only those not already revoked, saves once, and emits the established session-revocation events. Password code depends on this boundary, never on `ISessionRepository`.

## Complexity Tracking

None.
