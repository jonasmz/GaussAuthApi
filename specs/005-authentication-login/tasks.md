---

description: "Actionable implementation tasks for application-scoped authentication login"
---

# Tasks: Application-Scoped Authentication Login

**Input**: Design documents from `specs/005-authentication-login/`

**Prerequisites**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md), [data-model.md](data-model.md), [contracts/login-api.md](contracts/login-api.md), and [quickstart.md](quickstart.md)

**Tests**: Essential login success/failure, state-boundary, lockout, rate-limiting, and Identity-independence tests are included because the specification's Testing Strategy section explicitly requires them. Run all .NET commands in the existing `sdk` Docker service.

**Organization**: Tasks are sequential by design. The constitution prohibits parallel execution merely because files differ; `LoginService` is the single orchestration point for every user story's acceptance behavior, so one coherent implementation path is required.

## Phase 1: Setup

**Purpose**: Confirm feature-001..004 baseline and established conventions before adding the authentication use case.

- [ ] T001 Review `specs/005-authentication-login/spec.md`, `plan.md`, `data-model.md`, `contracts/login-api.md`, and `research.md`; record no deviation from the clarified application-code-only, uniform-failure, and lockout/rate-limiting-separation rules in `specs/005-authentication-login/tasks.md`.
- [ ] T002 Inspect `src/GaussAuth.Infrastructure/Identity/identityCredentialProvisioningService.service.cs`, `src/GaussAuth.Infrastructure/DependencyInjection/infrastructureServiceCollectionExtensions.extension.cs`, `src/GaussAuth.Api/DependencyInjection/apiServiceCollectionExtensions.extension.cs`, `src/GaussAuth.Api/DependencyInjection/safeExceptionHandler.handler.cs`, `src/GaussAuth.Api/Program.cs`, and existing feature-002/003 repositories and DTOs (`createUserRequest.dto.cs`, `createApplicationRequest.dto.cs`) to preserve composition, safe-error, field-limit, and rate-limiting conventions.

---

## Phase 2: Foundational (Credential Verification Pipeline)

**Purpose**: Establish the minimal, focused ports and Identity wiring the login use case needs, without yet implementing login behavior itself.

**⚠️ CRITICAL**: Complete this phase before any user-story work.

- [ ] T003 Add `Task<User?> GetByNormalizedEmailAsync(string normalizedEmail, CancellationToken cancellationToken)` to `src/GaussAuth.Application/Users/Ports/userRepository.interface.cs`.
- [ ] T004 Implement `GetByNormalizedEmailAsync` against the existing normalized-email index in `src/GaussAuth.Infrastructure/Persistence/userRepository.repository.cs`.
- [ ] T005 Define `ICredentialVerificationService` with `Task<CredentialVerificationOutcome> VerifyPasswordAsync(Guid userId, string password, CancellationToken cancellationToken)`, where `CredentialVerificationOutcome` is a closed `Success`/`InvalidPassword`/`LockedOut` enum, in `src/GaussAuth.Application/Login/Ports/credentialVerificationService.interface.cs`.
- [ ] T006 Register `SignInManager<IdentityUser<Guid>>` via `.AddSignInManager()` on the existing `AddIdentityCore<IdentityUser<Guid>>()` chain in `src/GaussAuth.Infrastructure/DependencyInjection/infrastructureServiceCollectionExtensions.extension.cs`.
- [ ] T007 Implement `IdentityCredentialVerificationService` using `UserManager.FindByIdAsync` to locate the Identity row by the shared `Guid` id, then `SignInManager.CheckPasswordSignInAsync(identityUser, password, lockoutOnFailure: true)`, mapping the Identity result to `CredentialVerificationOutcome` without exposing password hashes or Identity internals, in `src/GaussAuth.Infrastructure/Identity/identityCredentialVerificationService.service.cs`.
- [ ] T008 Register `ICredentialVerificationService` → `IdentityCredentialVerificationService` with the existing scoped lifetime convention in `src/GaussAuth.Infrastructure/DependencyInjection/infrastructureServiceCollectionExtensions.extension.cs`.

**Checkpoint**: Password verification is reachable only through a minimal Application-layer port; Domain/Application reference no Identity types.

---

## Phase 3: User Story 1 - Authenticate Into an Active Application (Priority: P1) 🎯 MVP

**Goal**: A registered user with an active account, an active target application, and an active membership can log in with email and password and receive the stable identifiers needed by `006-sessions-access`.

**Independent Test**: Create an active user, an active application, and an active membership; submit the application code, normalized email, and correct password; confirm `200` with the stable user/application identifiers and no credential content anywhere in the result.

### Tests for User Story 1

- [ ] T009 [US1] Add integration test for successful login (active user/application/membership, correct password) returning `200` with the stable `userId`/`applicationId` in `tests/GaussAuth.Foundation.Tests/authenticationLoginTests.test.cs`.
- [ ] T010 [US1] Add integration test proving an email submitted in a different case or with surrounding whitespace that normalizes to the same identity still authenticates successfully in `tests/GaussAuth.Foundation.Tests/authenticationLoginTests.test.cs`.
- [ ] T011 [US1] Add test asserting no password, password hash, or other Identity credential detail appears in the success response body in `tests/GaussAuth.Foundation.Tests/authenticationLoginTests.test.cs`.

### Implementation for User Story 1

- [ ] T012 [US1] Add `LoginOperationResult` with `Success(Guid userId, Guid applicationId)` and `Failure` states, carrying an internal-only failure reason not exposed on the type's public surface, in `src/GaussAuth.Application/Login/loginOperationResult.result.cs`.
- [ ] T013 [US1] Implement `LoginService.AuthenticateAsync(string applicationCode, string email, string password, CancellationToken)` in `src/GaussAuth.Application/Login/loginService.service.cs`: normalize the email via `ICredentialProvisioningService.NormalizeEmail`, resolve the user via `GetByNormalizedEmailAsync`, resolve the application via `IApplicationRepository.GetByCodeAsync`, resolve the membership via `IApplicationMembershipRepository.GetAsync`, require user/application/membership all active and the application/membership to match the requested application (FR-004, FR-005, FR-006), verify the password via `ICredentialVerificationService` only after the other prerequisites are known, map every unmet condition to the same `Failure` outcome, and log only identifiers and outcome — never the password.
- [ ] T014 [US1] Create `LoginRequest` (`ApplicationCode` required max 64 chars, `Email` required `[EmailAddress]` max 256 chars, `Password` required max 128 chars) and `LoginResponse` (`UserId`, `ApplicationId`) DTOs in `src/GaussAuth.Api/Login/loginRequest.dto.cs` and `src/GaussAuth.Api/Login/loginResponse.dto.cs`.
- [ ] T015 [US1] Implement `POST /auth/login` in `src/GaussAuth.Api/Login/loginEndpoints.extension.cs` per `contracts/login-api.md`: `200` + `LoginResponse` on success, uniform `401` Problem Details on any `LoginOperationResult.Failure` (no body detail distinguishing cause), `400` Problem Details on request-validation failure, and never echo the submitted password.
- [ ] T016 [US1] Register `LoginService` in `src/GaussAuth.Api/DependencyInjection/applicationServiceCollectionExtensions.extension.cs` and map `MapLoginEndpoints()` in `src/GaussAuth.Api/Program.cs`.

**Checkpoint**: A valid user can authenticate end-to-end for an active application with an active membership, and the Application layer depends on no Identity type.

---

## Phase 4: User Story 2 - Reject Authentication for Invalid Credentials or Unknown Accounts (Priority: P1)

**Goal**: Prove that an unknown email and an incorrect password are rejected with the exact same externally observable outcome, revealing nothing about account existence.

**Independent Test**: Attempt login with a non-existent email and, separately, a valid email with a wrong password; confirm both attempts produce the identical `401` shape.

### Tests for User Story 2

- [ ] T017 [US2] Add integration test proving an unknown email returns the uniform `401` failure defined in `contracts/login-api.md` in `tests/GaussAuth.Foundation.Tests/authenticationLoginTests.test.cs`.
- [ ] T018 [US2] Add integration test proving an incorrect password for a known email returns a `401` response identical in status and body shape to T017 in `tests/GaussAuth.Foundation.Tests/authenticationLoginTests.test.cs`.
- [ ] T019 [US2] Add test proving invalid input (missing email/password, malformed email, oversized `applicationCode`/`email`/`password`) returns `400` without echoing the submitted password in the response in `tests/GaussAuth.Foundation.Tests/authenticationLoginTests.test.cs`.

### Implementation for User Story 2

No additional implementation tasks: the uniform-failure behavior under test here is already produced by `LoginService` (T013) and the endpoint's failure mapping (T015) completed in User Story 1; this phase verifies that unified behavior.

**Checkpoint**: Credential rejection is uniform and does not leak account existence.

---

## Phase 5: User Story 3 - Enforce Account, Application, and Membership State Boundaries (Priority: P1)

**Goal**: Prove that an inactive user, an inactive/missing application, or a missing/inactive membership independently blocks login without reactivating anything, and that membership state never crosses application boundaries.

**Independent Test**: Build a user with valid credentials and an active membership only in application A; confirm login succeeds only for A. Deactivate the user, the application, and the membership independently and confirm each alone blocks login while correct credentials remain unchanged.

### Tests for User Story 3

- [ ] T020 [US3] Add integration test proving an inactive user is rejected with the uniform `401` failure and is not reactivated by the login attempt in `tests/GaussAuth.Foundation.Tests/authenticationLoginTests.test.cs`.
- [ ] T021 [US3] Add integration test proving a nonexistent or inactive target application is rejected with the uniform `401` failure even though the membership record still exists in `tests/GaussAuth.Foundation.Tests/authenticationLoginTests.test.cs`.
- [ ] T022 [US3] Add integration test proving a missing or inactive membership is rejected with the uniform `401` failure in `tests/GaussAuth.Foundation.Tests/authenticationLoginTests.test.cs`.
- [ ] T023 [US3] Add integration test proving a user with an active membership only in application A cannot log in to application B, and that membership state in one application never affects the outcome in another in `tests/GaussAuth.Foundation.Tests/authenticationLoginTests.test.cs`.

### Implementation for User Story 3

No additional implementation tasks: the state-boundary checks under test here (FR-004, FR-005, FR-006) are already enforced by `LoginService` (T013) completed in User Story 1; this phase verifies that enforcement.

**Checkpoint**: Access boundaries established by prior features (`002`, `003`) remain intact and application-isolated through login.

---

## Phase 6: User Story 4 - Protect the Login Endpoint from Abuse (Priority: P2)

**Goal**: Repeated invalid password attempts against one account lock it out via ASP.NET Core Identity, and the login endpoint itself rejects excessive requests from one source before credential validation runs — both with externally configurable thresholds.

**Independent Test**: Submit repeated incorrect passwords for one account until lockout engages and confirm a subsequent correct-password attempt still fails; separately, submit requests beyond the configured rate limit from one source and confirm later requests in the same window are rejected before reaching credential validation.

### Tests for User Story 4

- [ ] T024 [US4] Add integration test proving repeated invalid-password attempts reach the configured lockout threshold and a subsequent correct-password attempt still returns the uniform `401` failure in `tests/GaussAuth.Foundation.Tests/authenticationLoginTests.test.cs`.
- [ ] T025 [US4] Add integration test proving login requests exceeding the configured `login` rate limit within the configured window receive `429` with a `Retry-After` header before credential validation occurs in `tests/GaussAuth.Foundation.Tests/authenticationLoginTests.test.cs`.

### Implementation for User Story 4

- [ ] T026 [US4] Configure `IdentityOptions.Lockout.MaxFailedAccessAttempts` (from `Identity:Lockout:MaxFailedAccessAttempts`, default `5`), `Lockout.DefaultLockoutTimeSpan` (from `Identity:Lockout:DefaultLockoutMinutes`, default `5`), and `Lockout.AllowedForNewUsers = true` in the existing `AddIdentityCore<IdentityUser<Guid>>()` options callback in `src/GaussAuth.Infrastructure/DependencyInjection/infrastructureServiceCollectionExtensions.extension.cs`.
- [ ] T027 [US4] Add a `"login"` fixed-window rate-limiting policy partitioned by `RemoteIpAddress`, configurable via `RateLimiting:Login:PermitLimit` (default `5`) and `RateLimiting:Login:WindowSeconds` (default `60`), returning `429` with `Retry-After`, mirroring the existing `"user-creation"` policy in `src/GaussAuth.Api/DependencyInjection/apiServiceCollectionExtensions.extension.cs`.
- [ ] T028 [US4] Apply `.RequireRateLimiting("login")` to the `POST /auth/login` route in `src/GaussAuth.Api/Login/loginEndpoints.extension.cs`.

**Checkpoint**: Brute-force/credential-stuffing (lockout) and endpoint-level abuse (rate limiting) are both constrained as two independent, configurable protections.

---

## Phase 7: User Story 5 - Record Authentication Security Events (Priority: P3)

**Goal**: Every meaningful authentication outcome (success, failure, lockout) produces an internal security event with safe metadata only, never credential content.

**Independent Test**: Trigger one successful login and one failed login; confirm each produces a recorded event with only safe metadata (user id when known, application id when known, outcome category) and no password or credential content.

### Tests for User Story 5

- [ ] T029 [US5] Add test proving a successful login records a `LoginSucceeded` security event carrying only `userId`, `applicationId`, and outcome metadata in `tests/GaussAuth.Foundation.Tests/authenticationLoginTests.test.cs`.
- [ ] T030 [US5] Add test proving a failed login records a `LoginFailed` security event, and a lockout-triggered rejection records an `AccountLockedOut` security event, with no password or password hash present in either in `tests/GaussAuth.Foundation.Tests/authenticationLoginTests.test.cs`.

### Implementation for User Story 5

- [ ] T031 [US5] Define `SecurityEventType` enum (`LoginSucceeded`, `LoginFailed`, `AccountLockedOut`) in `src/GaussAuth.Application/Security/securityEventType.enum.cs` and `ISecurityEventRecorder` with `Task RecordAsync(SecurityEventType type, Guid? userId, Guid? applicationId, CancellationToken cancellationToken)` in `src/GaussAuth.Application/Security/Ports/securityEventRecorder.interface.cs`.
- [ ] T032 [US5] Implement `LoggingSecurityEventRecorder` writing one structured log entry per call (event type, optional `userId`, optional `applicationId`) through standard `ILogger` abstractions, with no credential content, in `src/GaussAuth.Infrastructure/Security/loggingSecurityEventRecorder.service.cs`.
- [ ] T033 [US5] Register `ISecurityEventRecorder` → `LoggingSecurityEventRecorder` with the existing scoped lifetime convention in `src/GaussAuth.Infrastructure/DependencyInjection/infrastructureServiceCollectionExtensions.extension.cs`.
- [ ] T034 [US5] Inject `ISecurityEventRecorder` into `LoginService` and record `LoginSucceeded`, `LoginFailed`, or `AccountLockedOut` at the corresponding decision point using the internal failure reason already tracked on `LoginOperationResult` (T012), in `src/GaussAuth.Application/Login/loginService.service.cs`.

**Checkpoint**: Authentication attempts are auditable through a minimal, swappable port, compatible with the complete `009-security-audit` subsystem later.

---

## Phase 8: Polish and Cross-Cutting Validation

**Purpose**: Validate architecture, safe contracts, and end-to-end behavior for the completed feature.

- [ ] T035 Verify every new C# source file has exactly one top-level type and a `<name>.<type>.cs` filename; extend `tests/GaussAuth.Foundation.Tests/architectureTests.test.cs` only if the existing boundary assertion does not cover the new `Login`/`Security` dependencies.
- [ ] T036 Review `src/GaussAuth.Api/Login/`, `src/GaussAuth.Application/Login/`, `src/GaussAuth.Application/Security/`, and `src/GaussAuth.Infrastructure/Identity/` for exact field limits, the uniform safe failure contract, absence of password/credential content in logs/responses/exceptions, identifier/outcome-only logging, and absence of session, token, MFA, OAuth 2.0, or OpenID Connect code.
- [ ] T037 Run the full Docker test suite and the API curl scenarios from `specs/005-authentication-login/quickstart.md`; correct only feature-005 artifacts and failures found.

## Dependencies and Execution Order

- Phase 1 precedes Phase 2.
- Phase 2 blocks every user story because the credential-verification port and Identity wiring are shared.
- US1 (Phase 3) implements the complete login use case and is the MVP; it already enforces the FR-004/FR-005/FR-006 prerequisites.
- US2 (Phase 4) and US3 (Phase 5) verify behavior already produced by US1; they add no new production code.
- US4 (Phase 6) adds configurable lockout thresholds and the dedicated rate-limiting policy, independent of US2/US3.
- US5 (Phase 7) adds security-event recording on top of the completed `LoginService` and depends on US1.
- Phase 8 follows all requested stories.

## Parallel Opportunities

None are scheduled. `LoginService` is the single orchestration point every story's acceptance behavior depends on, and the project constitution requires one sequential implementation stream rather than parallel execution merely because some files differ.

## Implementation Strategy

1. Complete the shared credential-verification port and Identity wiring (Phase 2).
2. Deliver the complete, independently testable login use case (US1) — this is the MVP.
3. Verify the uniform failure contract (US2) and the state-boundary enforcement (US3) already produced by US1.
4. Add configurable lockout and dedicated rate limiting (US4).
5. Add minimal, swappable security-event recording (US5).
6. Validate architecture, safe contracts, and the full Docker test suite/quickstart scenarios.

## Format Validation

All 37 tasks use the required checkbox, sequential `T###` identifier, story labels only in user-story phases, and explicit repository-relative file paths. No `[P]` markers are used because sequential execution is required by project governance.
