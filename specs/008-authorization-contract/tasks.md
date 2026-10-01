# Tasks: Authorization Contract

**Input**: Design documents from `/specs/008-authorization-contract/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/authorization-context-api.md, quickstart.md

**Tests**: Essential integration and architecture tests are required by the specification. Run all .NET tests in the Docker SDK container.

**Organization**: Tasks are grouped by user story. The constitution requires sequential execution where the same boundaries and files are affected.

## Phase 1: Setup

**Purpose**: Verify contract inputs and external secret hygiene before implementation.

- [X] T001 Review `specs/008-authorization-contract/spec.md`, `plan.md`, `research.md`, `data-model.md`, `contracts/authorization-context-api.md`, and `quickstart.md` alongside existing Sessions and Authorization composition.
- [X] T002 Verify `.gitignore`, `.env.example`, and `compose.dev.yml` keep consumer service credentials external and provide no real Application secret in versioned configuration.

---

## Phase 2: Foundational

**Purpose**: Establish shared current-authorization and service-credential boundaries. This phase blocks every story.

- [X] T003 Add safe authorization-context security-event values and recorder coverage in `src/GaussAuth.Application/Security/securityEventType.enum.cs` and `tests/GaussAuth.Foundation.Tests/authorizationContractTests.test.cs`.
- [X] T004 Define `IConsumerCredentialValidator`, safe validation result, and authorization-context result contracts in `src/GaussAuth.Application/AuthorizationContext/Ports/` and `src/GaussAuth.Application/AuthorizationContext/`; no service secret or Infrastructure type may cross the port.
- [X] T005 Extend `src/GaussAuth.Application/Authorization/Ports/userRoleRepository.interface.cs` and `src/GaussAuth.Infrastructure/Persistence/userRoleRepository.repository.cs` with current active-role resolution scoped to `(UserId, ApplicationId)`; role records MUST expose stable ID/name only and exclude inactive assignment, membership, role, user, or application.
- [X] T006 Implement `ConfiguredConsumerCredentialValidator` in `src/GaussAuth.Infrastructure/AuthorizationContext/` using external `AuthorizationConsumers` configuration with one distinct current secret and optional retiring secret per Application code; reject cross-application use and configured secret reuse, use platform constant-time comparison, and never log or return secrets.
- [X] T007 Register the consumer credential validator and authorization-context service in `src/GaussAuth.Infrastructure/DependencyInjection/infrastructureServiceCollectionExtensions.extension.cs` and `src/GaussAuth.Api/DependencyInjection/applicationServiceCollectionExtensions.extension.cs`.
- [X] T008 Add configurable `authorization-context` credential-bearing rate limiting in `src/GaussAuth.Api/DependencyInjection/apiServiceCollectionExtensions.extension.cs` with `RateLimiting:AuthorizationContext:PermitLimit` default 600 and `WindowSeconds` default 60.

**Checkpoint**: Consumer authentication, current role lookup, and safe contract results are available without exposing service credentials.

---

## Phase 3: User Story 1 - Establish Current Authorization Context (Priority: P1) 🎯 MVP

**Goal**: A validated consumer resolves current UserId, ApplicationId, SessionId, expirations, active roles, and unique effective permissions from a valid user credential.

**Independent Test**: A test consumer calls the contract for an active user/session and receives only the documented context fields; invalid, expired, revoked, and ineligible access is uniformly rejected.

- [ ] T009 [US1] Add failing context integration scenarios in `tests/GaussAuth.Foundation.Tests/authorizationContractTests.test.cs` for valid identifiers/expirations, active roles, unique permissions, malformed/expired/revoked credential, inactive user/application/membership, public-field minimization, required/nonblank maximum-64 application code, and required/nonblank maximum-512 consumer secret.
- [ ] T010 [US1] Implement `AuthorizationContextService.ResolveAsync` in `src/GaussAuth.Application/AuthorizationContext/authorizationContextService.service.cs` by validating the consumer boundary, invoking authoritative `SessionService.ValidateAsync`, resolving active roles/effective permissions, and recording only identifier/result-category events.
- [ ] T011 [US1] Add response DTOs and map `POST /auth/authorization-context` in `src/GaussAuth.Api/AuthorizationContext/authorizationContextEndpoints.extension.cs` and `src/GaussAuth.Api/AuthorizationContext/authorizationContextResponse.dto.cs`; require the bearer user credential plus `X-GaussAuth-Application-Code` and `X-GaussAuth-Consumer-Secret`, and return only the documented safe `200`/generic `401` shapes.
- [ ] T012 [US1] Register authorization-context endpoints in `src/GaussAuth.Api/Program.cs` and verify the endpoint honors the default 600-per-60-second and overridden `authorization-context` rate-limit policy.

**Checkpoint**: A consumer can resolve an authoritative minimal context without querying Auth persistence.

---

## Phase 4: User Story 2 - Enforce Application Isolation (Priority: P1)

**Goal**: A consumer credential, user session, roles, and permissions are all restricted to exactly one application.

**Independent Test**: A credential/session for Application A is rejected by a Consumer B request; contexts never contain B roles or permissions.

- [ ] T013 [US2] Add cross-application integration scenarios in `tests/GaussAuth.Foundation.Tests/authorizationContractTests.test.cs` for wrong application code, Consumer B secret with Application A request, Consumer A secret with Application B request, and no cross-application role/permission leakage.
- [ ] T014 [US2] Harden Application-code matching and safe failure mapping in `src/GaussAuth.Application/AuthorizationContext/authorizationContextService.service.cs` and `src/GaussAuth.Api/AuthorizationContext/authorizationContextEndpoints.extension.cs` so every mismatch is generic `401` and emits no secret or internal state.

**Checkpoint**: Every cross-application authorization-context attempt fails safely.

---

## Phase 5: User Story 3 - Authorize a Business Operation by Permission (Priority: P1)

**Goal**: A separate consumer-equivalent boundary can permit or deny a generic operation using a returned permission without Auth database access.

**Independent Test**: The test consumer allows a synthetic operation only if a returned permission code exists, and current role/permission changes are reflected by its next context call.

- [ ] T015 [US3] Add consumer-boundary and authorization-freshness scenarios in `tests/GaussAuth.Foundation.Tests/authorizationContractTests.test.cs`: permit/deny by required permission, duplicate removal, assignment removal, role/permission deactivation, and no Auth persistence-model reference in the consumer fixture.
- [ ] T016 [US3] Implement or refine current authorization queries in `src/GaussAuth.Application/AuthorizationContext/authorizationContextService.service.cs` and `src/GaussAuth.Infrastructure/Persistence/userRoleRepository.repository.cs` so every next context lookup reflects current effective authorization without caching.

**Checkpoint**: A consumer-equivalent permission check works using only the published contract.

---

## Phase 6: User Story 4 - Integrate Without Auth Persistence Coupling (Priority: P2)

**Goal**: Consumers receive clear integration guidance and can retain logical UserId references without internal Auth coupling.

**Independent Test**: Documentation and architecture tests demonstrate that a consumer needs only configured application identity, its service credential, a user credential, and the public context DTO.

- [ ] T017 [US4] Add architecture checks in `tests/GaussAuth.Foundation.Tests/architectureTests.test.cs` that public authorization-context DTOs expose no EF, Identity, Session entity, security stamp, profile, or secret fields and Application/Domain contain no Infrastructure dependency.
- [ ] T018 [US4] Finalize consumer setup, validation, permission-check, rejection, no-database-access, and rotation guidance in `specs/008-authorization-contract/contracts/authorization-context-api.md` and `specs/008-authorization-contract/quickstart.md` to match the implemented headers and response.

**Checkpoint**: An independent consumer can integrate without Auth persistence or internal-model coupling.

---

## Phase 7: Polish and Validation

- [ ] T019 Verify external-configuration startup validation, secret rotation (current plus retiring credential for the same application), rejection after retiring credential removal, and log hygiene in `tests/GaussAuth.Foundation.Tests/authorizationContractTests.test.cs`.
- [ ] T020 Verify no consumer service credential, bearer credential, password, hash, security stamp, or key material is committed or logged; update `tests/GaussAuth.Foundation.Tests/architectureTests.test.cs` only for feature-008 rules.
- [ ] T021 Run `dotnet test GaussAuth.slnx` inside the SDK Docker container and execute all scenarios in `specs/008-authorization-contract/quickstart.md`; correct only feature-008 artifacts.

## Dependencies & Execution Order

- Phase 1 precedes Phase 2.
- T003–T008 block every user story.
- US1 (T009–T012) is the MVP and establishes the usable public contract.
- US2 depends on US1 endpoint shape; US3 depends on US1 context data; US4 depends on the implemented contract.
- Polish follows every selected story.

## Execution Discipline

Execute tasks sequentially in their numbered order. The constitution requires one well-contextualized sequential implementation path; do not launch parallel work merely because files appear independent.

## Implementation Strategy

1. Complete Setup and Foundational boundaries, including external credential configuration validation.
2. Deliver US1 and validate a minimal authoritative context end to end.
3. Add isolation tests/hardening, then consumer permission/freshness behavior.
4. Finalize consumer documentation, rotation coverage, architecture/security checks, and full Docker validation.
