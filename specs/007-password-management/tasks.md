# Tasks: Password Management

**Input**: `specs/007-password-management/` design artifacts. All .NET commands run in the `sdk` Docker service. Tasks are sequential: the constitution disallows speculative parallel work.

## Phase 1: Setup

- [X] T001 Review `spec.md`, `plan.md`, `research.md`, `data-model.md`, `contracts/passwords-api.md`, `quickstart.md`, and existing Identity/Sessions composition before coding.
- [X] T002 Verify `.gitignore` excludes the configured protected recovery-delivery file and add only the required ignored pattern.
- [X] T002a Add documented empty delivery-path configuration to `.env.example` and `compose.dev.yml`; no delivery file path or credential is committed.

## Phase 2: Foundational

- [X] T003 Add password-management event values to `src/GaussAuth.Application/Security/securityEventType.enum.cs` and update existing recorder tests in `tests/GaussAuth.Foundation.Tests/`.
- [X] T004 Define focused Identity-facing password ports/results in `src/GaussAuth.Application/Passwords/Ports/` for change, recovery credential generation, reset, and safe outcomes; do not expose Identity types or tokens.
- [X] T005 Define `IRecoveryDelivery` in `src/GaussAuth.Application/Passwords/Ports/recoveryDelivery.interface.cs` and a delivery instruction type with user id and secret credential kept out of logs/events.
- [X] T006 Extend the Sessions Application boundary with an explicit per-user revoke-all operation in `src/GaussAuth.Application/Sessions/` and its port, preserving existing single-session behavior.
- [X] T007 Implement Identity password adapters in `src/GaussAuth.Infrastructure/Identity/` using framework change/reset/token/lockout APIs and map framework outcomes to safe Application results.
- [X] T008 Implement protected Development/Test file delivery in `src/GaussAuth.Infrastructure/Passwords/` and fail safely outside those environments without a configured delivery adapter.
- [X] T008a Add delivery-failure and protected-file exclusion tests in `tests/GaussAuth.Foundation.Tests/passwordManagementTests.test.cs`; failure keeps the public recovery response generic and never logs a credential.
- [X] T009 Register password adapters, delivery adapter, and session revoke-all service in `src/GaussAuth.Infrastructure/DependencyInjection/infrastructureServiceCollectionExtensions.extension.cs` and `src/GaussAuth.Api/DependencyInjection/applicationServiceCollectionExtensions.extension.cs`.
- [X] T010 Add configurable recovery/reset fixed-window policies in `src/GaussAuth.Api/DependencyInjection/apiServiceCollectionExtensions.extension.cs`.

## Phase 3: User Story 1 - Change My Password (P1) 🎯 MVP

**Goal**: An active user with a valid session changes only their own password after current-password verification, and every session is revoked.

**Independent Test**: Change password through the API; old password and old session fail, new password succeeds, and all user sessions are revoked.

- [X] T011 [US1] Add change-password integration scenarios in `tests/GaussAuth.Foundation.Tests/passwordManagementTests.test.cs` for success, wrong current password, policy rejection, inactive user, revoke-all behavior, and unchanged memberships/roles/permissions/application data.
- [X] T012 [US1] Implement `PasswordManagementService.ChangeAsync` in `src/GaussAuth.Application/Passwords/passwordManagementService.service.cs` using trusted session context, current-password verification, safe outcomes, revoke-all, and events.
- [X] T013 [US1] Create change request DTO and map `POST /auth/password/change` in `src/GaussAuth.Api/Passwords/` with required/max-128 fields, bearer validation, safe responses, and no target user id.
- [X] T014 [US1] Register password endpoints from `src/GaussAuth.Api/Program.cs` and run the full Docker test suite.

## Phase 4: User Story 2 - Request Password Recovery (P1)

**Goal**: Public recovery requests never reveal account state while eligible active accounts receive protected instructions.

**Independent Test**: Known, unknown, and inactive emails produce identical `202` responses; only eligible account writes a protected instruction; rate limit returns `429`.

- [X] T015 [US2] Add recovery-request integration and log-hygiene scenarios in `tests/GaussAuth.Foundation.Tests/passwordManagementTests.test.cs`.
- [X] T016 [US2] Implement `PasswordManagementService.RequestRecoveryAsync` in `src/GaussAuth.Application/Passwords/passwordManagementService.service.cs` with normalized email, active-user check, generic outcome, and safe event recording.
- [X] T017 [US2] Create recovery DTO and map `POST /auth/password/recovery` in `src/GaussAuth.Api/Passwords/` with required valid email/max-256, generic `202`, and recovery rate limiting.

## Phase 5: User Story 3 - Reset a Forgotten Password (P1)

**Goal**: A valid temporary recovery credential establishes a new password, clears lockout, and revokes all sessions.

**Independent Test**: Valid reset makes old password fail, new password work, and sessions fail; invalid/reused credentials have identical safe failure and change nothing.

- [ ] T018 [US3] Add reset integration scenarios in `tests/GaussAuth.Foundation.Tests/passwordManagementTests.test.cs` for valid, invalid/reused, inactive, policy failure, lockout clear, revoke-all, reset rate limit, and unchanged memberships/roles/permissions/application data.
- [ ] T019 [US3] Implement `PasswordManagementService.ResetAsync` in `src/GaussAuth.Application/Passwords/passwordManagementService.service.cs` with safe failure mapping, lockout clear only after success, revoke-all, and events.
- [ ] T020 [US3] Create reset DTO and map `POST /auth/password/reset` in `src/GaussAuth.Api/Passwords/` with required/max 256 email, 4096 credential, 128 password; generic safe `400`; reset rate limiting.

## Phase 6: User Story 4 - Observe Password Events (P2)

- [ ] T021 [US4] Extend recording/capturing tests in `tests/GaussAuth.Foundation.Tests/passwordManagementTests.test.cs` to assert password lifecycle and session-revocation events contain identifiers only and no secret values.
- [ ] T022 [US4] Add structured safe logs/events in `src/GaussAuth.Application/Passwords/passwordManagementService.service.cs` for success, recovery request, and useful reset failure categories.

## Phase 7: Polish

- [ ] T023 Verify Domain/Application Identity-reference restrictions in `tests/GaussAuth.Foundation.Tests/architectureTests.test.cs`, one-top-level-type naming, and no credential/hash/token in versioned files.
- [ ] T024 Run `dotnet test GaussAuth.slnx` in the SDK container and execute `specs/007-password-management/quickstart.md`; correct only feature-007 artifacts.

## Dependencies & Execution Order

`T001–T010` block all stories. US1 is the MVP. US2 depends on shared password/delivery foundation. US3 depends on recovery credential generation. US4 follows all lifecycle operations. Polish follows all stories.
