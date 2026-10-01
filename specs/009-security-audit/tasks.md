# Tasks: Security Audit

**Input**: Design documents from `/specs/009-security-audit/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/security-events-api.md, quickstart.md

**Tests**: Essential security integration, architecture, migration, and regression tests are required by the specification. Run them in the Docker SDK container.

**Organization**: Tasks are grouped by user story and must execute sequentially because the existing persistence and security boundaries overlap.

## Phase 1: Setup

**Purpose**: Establish the documented configuration and review baseline.

- [X] T001 Review `specs/009-security-audit/spec.md`, `plan.md`, `research.md`, `data-model.md`, `contracts/security-events-api.md`, `quickstart.md`, and `security-review.md` against existing security, persistence, and authorization composition.
- [X] T002 Verify `.gitignore`, `.env.example`, and `compose.dev.yml` keep plaintext consumer secrets, hashed representations, database credentials, signing keys, and recovery material outside versioned configuration.

---

## Phase 2: Foundational

**Purpose**: Create the append-only SecurityEvent boundary and persistence substrate required by all stories.

- [X] T003 Add immutable SecurityEvent domain/entity and stable outcome/category contracts in `src/GaussAuth.Domain/Security/` with server-controlled UTC timestamp, optional known IDs, optional correlation ID maximum 128 characters, and no mutation transition.
- [X] T004 Extend `src/GaussAuth.Application/Security/` and `src/GaussAuth.Application/Security/Ports/` with central catalog/draft/recorder/query/transaction contracts enforcing event type maximum 128 characters, allow-listed context, and metadata maximum 2 KiB.
- [X] T005 Add SecurityEvents mapping, indexes for newest-first global/application/user/session/type paths, append-only repository, and EF migration in `src/GaussAuth.Infrastructure/Persistence/` with nullable application ownership for global Auth events and no cascade evidence deletion.
- [X] T006 Replace logging-only registration with persisted recorder, safe operational-failure logger, correlation source, validated configurable `SecurityAudit:RetentionDays` policy (without a normal-flow delete or purge scheduler), and required Application/Infrastructure registrations in `src/GaussAuth.Infrastructure/Security/` and dependency-injection extensions.
- [X] T007 Add foundational persistence/immutability/catalog safety tests in `tests/GaussAuth.Foundation.Tests/securityAuditTests.test.cs` covering no prohibited credential/header/request values and no normal event mutation surface.

**Checkpoint**: SecurityEvent writes are durable, safe, append-only, and available to all application slices.

---

## Phase 3: User Story 1 - Review Security Activity (Priority: P1) 🎯 MVP

**Goal**: Authorized reviewers query a bounded, isolated, immutable security history.

**Independent Test**: Generate representative events, query deterministic cursor pages, and verify application scope cannot view another application's or global events while explicit global review can.

- [ ] T008 [US1] Add failing audit-query contract/integration scenarios in `tests/GaussAuth.Foundation.Tests/securityAuditTests.test.cs` for safe fields, newest-first `(OccurredAtUtc, Id)` cursor ordering, default 50/max 100 pagination, filters, no-store, invalid requests, immutable routes, application isolation, and global reviewer visibility.
- [ ] T009 [US1] Implement `SecurityEventQueryService`, `AuditQuery`, and `AuthorizedAuditScope` in `src/GaussAuth.Application/Security/` so application scope is forced before repository access and global scope includes global Auth events only for the explicit reviewer capability.
- [ ] T010 [US1] Implement keyset query repository and separately configured global-reviewer identity validation in `src/GaussAuth.Infrastructure/Security/` and `src/GaussAuth.Infrastructure/Persistence/`.
- [ ] T011 [US1] Add safe request/response DTOs and `GET /security-events` mapping in `src/GaussAuth.Api/Security/` requiring valid bearer session, effective `audit.events.read` for application scope, `400` invalid filters, `401` invalid session, `403` invalid scope, `429` rate limit, and `Cache-Control: no-store`.
- [ ] T012 [US1] Register audit query authorization/rate limiting in `src/GaussAuth.Api/DependencyInjection/` and `src/GaussAuth.Api/Program.cs` with configurable bounded policy and no role-name/global-scope inference.

**Checkpoint**: Security history is independently queryable with deterministic bounded pages and strict scope isolation.

---

## Phase 4: User Story 2 - Preserve Safe Authentication and Authorization Boundaries (Priority: P1)

**Goal**: Relevant current and administrative activity is audited while existing authentication, sessions, passwords, consumer authentication, and isolation remain safe.

**Independent Test**: Exercise critical and operational actions, force recorder failure, and verify atomic rollback versus safe operational continuation plus existing rejection/rate/lockout/isolation behavior.

- [ ] T013 [US2] Add failing critical-versus-operational recorder-failure and representative event-category scenarios in `tests/GaussAuth.Foundation.Tests/securityAuditTests.test.cs` for login, lockout, sessions, passwords, lifecycle, authorization administration, consumer rejection, and context validation.
- [ ] T014 [US2] Integrate centrally cataloged operational SecurityEvent drafts into `src/GaussAuth.Application/Login/`, `Sessions/`, `Passwords/`, and `AuthorizationContext/` for login/rejection/lockout observation, recovery requests, expired-or-revoked-session access, consumer-authentication failures, and application-mismatch/context rejection with safe outcome/reason/trace data and observable non-blocking recorder failures.
- [ ] T015 [US2] Integrate cataloged critical event creation and shared transaction behavior into `src/GaussAuth.Application/Users/`, `Applications/`, `Memberships/`, `Roles/`, `Permissions/`, `Authorization/`, `Passwords/`, and `Sessions/` so lifecycle, privilege, password/reset credential changes, and administrative session revocations are incomplete when their audit event cannot persist.
- [ ] T016 [US2] Replace consumer plaintext configuration validation with `CurrentSecretHash` and optional `RetiringSecretHash` verification using approved Identity `PasswordHasher` in `src/GaussAuth.Infrastructure/AuthorizationContext/configuredConsumerCredentialValidator.service.cs`; reject legacy plaintext keys and preserve active-plus-retiring rotation and generic cross-application failure.
- [ ] T017 [US2] Update consumer configuration/DI and add startup, rotation, application-isolation, log/event secrecy, and invalid-hash regression coverage in `src/GaussAuth.Infrastructure/DependencyInjection/`, `.env.example`, and `tests/GaussAuth.Foundation.Tests/authorizationContractTests.test.cs`.
- [ ] T018 [US2] Add security regressions in `tests/GaussAuth.Foundation.Tests/` for configured endpoint limits, Identity lockout, enumeration-resistant login/recovery, session expiry/revocation/logout, inactive user/application/membership, cross-application roles/permissions/sessions/context, and safe expected errors.

**Checkpoint**: Security-sensitive state changes are audit-consistent, operational observations remain available, consumer secrets are one-way verified, and prior security boundaries remain intact.

---

## Phase 5: User Story 3 - Operate the API Safely (Priority: P2)

**Goal**: Operators deploy and run the API with safe HTTP behavior, bounded input, correlation, secret hygiene, and documented posture.

**Independent Test**: Send oversized/malformed sensitive input and representative failures, inspect headers/log/event data, and validate deployment documentation contains actual protections and limitations.

- [ ] T019 [US3] Add API hardening middleware for `X-Content-Type-Options: nosniff`, `Referrer-Policy: no-referrer`, built-in correlation propagation, and production HTTPS/HSTS behavior in `src/GaussAuth.Api/DependencyInjection/` and `src/GaussAuth.Api/Program.cs`; omit browser-only headers without API value.
- [ ] T020 [US3] Add configurable request-body and input limits for public/sensitive credentials, password/recovery inputs, audit filters/cursors/page sizes, and authorization-context headers in `src/GaussAuth.Api/` and `src/GaussAuth.Application/` before unnecessary processing.
- [ ] T021 [US3] Add architecture and integration checks in `tests/GaussAuth.Foundation.Tests/architectureTests.test.cs` and `securityAuditTests.test.cs` proving secrets/tokens/passwords/hashes/stamps/keys/connection data never enter SecurityEvents, logs, DTOs, or external errors and that structured logs resist user-controlled log injection.
- [ ] T022 [US3] Finalize actual endpoint, rate-limit, lockout, session/revocation, consumer hash/rotation, audit retention, database least-privilege, HTTPS, header, and accepted-limitation guidance in `specs/009-security-audit/security-review.md`, `quickstart.md`, and `contracts/security-events-api.md`.

**Checkpoint**: Operators have verified API-safe defaults and a concise, accurate security posture document.

---

## Phase 6: Polish and Validation

- [ ] T023 Verify SecurityEvent migration/indexes and all existing critical uniqueness/referential constraints in `tests/GaussAuth.Foundation.Tests/migrationTests.test.cs` and `tests/GaussAuth.Foundation.Tests/securityAuditTests.test.cs`.
- [ ] T024 Run `dotnet test GaussAuth.slnx` inside the SDK Docker container and execute every scenario in `specs/009-security-audit/quickstart.md`; correct only feature-009 artifacts.
- [ ] T025 Perform a final source/config/log hygiene scan for feature-009 sensitive values and validate `git diff --check` against `specs/009-security-audit/`, `src/`, and `tests/`.

## Dependencies & Execution Order

- Phase 1 precedes Phase 2.
- T003–T007 block all stories.
- US1 depends on durable events and query contracts.
- US2 depends on the common recorder/catalog and must complete before final operational validation.
- US3 depends on the security/query behavior it validates.
- Polish follows all stories.

## Execution Discipline

Execute tasks sequentially in numerical order. The constitution requires one contextual path because event writes, shared DbContext transactions, security headers, and regression tests overlap. Do not mark tasks parallel merely because their files differ.

## Implementation Strategy

1. Establish the durable, safe audit foundation.
2. Deliver scoped read-only audit queries as the MVP.
3. Add categorized event coverage, transaction policy, and one-way consumer-secret migration.
4. Harden API operations, document posture, then run the complete Docker validation suite.
