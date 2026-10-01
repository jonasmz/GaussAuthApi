---

description: "Actionable implementation tasks for authenticated sessions and access credentials"
---

# Tasks: Authenticated Sessions and Access Credentials

**Input**: Design documents from `specs/006-sessions-access/`

**Prerequisites**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md), [data-model.md](data-model.md), [contracts/sessions-api.md](contracts/sessions-api.md), and [quickstart.md](quickstart.md)

**Tests**: Essential session lifecycle, validation, revocation, isolation, eligibility, tampering, log-hygiene, persistence, and architecture tests are included because the specification's Testing Strategy section explicitly requires them. No token-library tests. Run every .NET command in the existing `sdk` Docker service from the repository root, using `docker compose --env-file .env -f compose.dev.yml run --rm sdk <command>` (referred to below as "in the `sdk` container"); do not run global Docker cleanup. Each `run --rm` container has an ephemeral NuGet cache (`HOME=/tmp`), so never pass `--no-restore`; run `dotnet build`/`dotnet test` with their default implicit restore (or chain restore and build in one `sh -c` invocation).

**Organization**: Tasks are sequential by design. The constitution prohibits parallel execution merely because files differ, and `SessionService` is the single orchestration point for every story, so no `[P]` markers are used. Each user story phase leaves the solution building and the full test suite green.

## Phase 1: Setup

**Purpose**: Confirm the 001-005 baseline, the clarified decisions, and the single new dependency before adding code.

- [X] T001 Review `specs/006-sessions-access/spec.md`, `plan.md`, `research.md` (R1-R15), `data-model.md`, and `contracts/sessions-api.md`; record in this file's Format Validation section any deviation from the clarified decisions (ES256 signed token linked to a persisted session by `sid`, roles/permissions never in the credential, deactivation effective at the next authoritative check without revoking, renewal with the current valid credential and no refresh token, 15-minute credential and 8-hour absolute session defaults). Record "no deviation" if none.
  - Result (2026-10-01): no deviation from the clarified decisions. Inspection (T002) found only two test-suite facts the plan had not listed, now reflected in T030 and T031: two existing schema tests hard-code four migrations, the exact table set, and (in `usersSchemaMigrationTests.test.cs`) a guard forbidding any table name containing "Session"; and the existing startup-failure tests start the API as a child process rather than through `WebApplicationFactory`.
- [X] T002 Inspect `src/GaussAuth.Application/Login/loginService.service.cs`, `src/GaussAuth.Application/Login/loginOperationResult.result.cs`, `src/GaussAuth.Application/Security/`, `src/GaussAuth.Domain/Memberships/applicationMembership.entity.cs` (entity style: private setters, private parameterless constructor, `Create` factory), `src/GaussAuth.Infrastructure/Persistence/authenticationDbContext.context.cs`, `src/GaussAuth.Infrastructure/Persistence/userRoleRepository.repository.cs`, the latest migration (`addRolesAndPermissions.migration.cs`, `addRolesAndPermissions.designer.cs`, `authenticationDbContextModelSnapshot.snapshot.cs`), `src/GaussAuth.Infrastructure/DependencyInjection/infrastructureServiceCollectionExtensions.extension.cs`, `src/GaussAuth.Api/Login/`, `src/GaussAuth.Api/DependencyInjection/`, `src/GaussAuth.Api/Program.cs`, `tests/GaussAuth.Foundation.Tests/authenticationLoginTests.test.cs` (factory and helper conventions, `ManagedEnvironmentKeys`), `startupTests.test.cs`, `migrationTests.test.cs`, and `architectureTests.test.cs` to preserve composition, naming, safe-error, one-type-per-file, and test conventions.
- [X] T003 Add `<PackageReference Include="Microsoft.IdentityModel.JsonWebTokens" Version="8.23.0" />` to `src/GaussAuth.Infrastructure/GaussAuth.Infrastructure.csproj` only (no package may be added to Domain, Application, or Api), then run `dotnet restore GaussAuth.slnx` in the `sdk` container and confirm it succeeds.

---

## Phase 2: Foundational (Session Model, Ports, Persistence, and Token Adapter)

**Purpose**: Establish the Domain entity, Application ports, persistence, signing key, token adapter, configuration, and test infrastructure that every user story needs, without yet implementing any use case.

**⚠️ CRITICAL**: Complete this phase before any user-story work.

### Domain

- [ ] T004 Define the `SessionState` enum with exactly `Active`, `Expired`, `Revoked` in `src/GaussAuth.Domain/Sessions/sessionState.enum.cs`.
- [ ] T005 Define the `Session` entity in `src/GaussAuth.Domain/Sessions/session.entity.cs` in the style of `ApplicationMembership` (private setters, private parameterless constructor for EF, `Create` factory, `ArgumentException` for invalid input): properties `Id` (`Guid`, "stable unique session identifier; never `Guid.Empty`"), `UserId` (`Guid`, never `Guid.Empty`), `ApplicationId` (`Guid`, never `Guid.Empty`), `CreatedAt` (`DateTimeOffset`), `ExpiresAt` (`DateTimeOffset`, absolute, "`CreatedAt + lifetime`, fixed at creation; never extended"), `RevokedAt` (`DateTimeOffset?`, "`null` while not revoked; set once, durable"); `Session.Create(Guid id, Guid userId, Guid applicationId, DateTimeOffset now, TimeSpan lifetime)` rejects empty identifiers and a non-positive `lifetime` and sets `ExpiresAt = now + lifetime`; `Revoke(DateTimeOffset now)` sets `RevokedAt` only if still `null` (repeated calls leave the first value untouched); `GetState(DateTimeOffset now)` returns `Revoked` if `RevokedAt` is set (takes precedence even when also past expiry), otherwise `Expired` if `now >= ExpiresAt`, otherwise `Active`. No last-activity or client-metadata property, and no reference to tokens, HTTP, EF Core, or Identity.

### Application ports and types

- [ ] T006 Define `AccessCredentialClaims` as a record (`Guid SessionId`, `Guid UserId`, `Guid ApplicationId`, `DateTimeOffset IssuedAt`, `DateTimeOffset ExpiresAt`) in `src/GaussAuth.Application/Sessions/Ports/accessCredentialClaims.result.cs`.
- [ ] T007 Define `PublicSigningKey` as a record (`string KeyId`, `string KeyType`, `string Curve`, `string Algorithm`, `string Use`, `string X`, `string Y`) in `src/GaussAuth.Application/Sessions/Ports/publicSigningKey.result.cs`.
- [ ] T008 Define `IAccessCredentialIssuer` with `string Issue(AccessCredentialClaims claims)` in `src/GaussAuth.Application/Sessions/Ports/accessCredentialIssuer.interface.cs`.
- [ ] T009 Define `IAccessCredentialValidator` with `Task<AccessCredentialClaims?> ValidateAsync(string credential, CancellationToken cancellationToken)` in `src/GaussAuth.Application/Sessions/Ports/accessCredentialValidator.interface.cs`; XML-document that `null` means unauthentic or malformed and that the method does **not** judge lifetime.
- [ ] T010 Define `IAccessCredentialKeySet` with `IReadOnlyList<PublicSigningKey> GetPublicKeys()` in `src/GaussAuth.Application/Sessions/Ports/accessCredentialKeySet.interface.cs`.
- [ ] T011 Define `ISessionRepository` with `Task AddAsync(Session session, CancellationToken cancellationToken)`, `Task<Session?> GetByIdAsync(Guid id, CancellationToken cancellationToken)`, and `Task SaveChangesAsync(CancellationToken cancellationToken)` in `src/GaussAuth.Application/Sessions/Ports/sessionRepository.interface.cs`.
- [ ] T012 Define the immutable `SessionPolicy` record (`TimeSpan SessionLifetime`, `TimeSpan AccessCredentialLifetime`) in `src/GaussAuth.Application/Sessions/sessionPolicy.options.cs`; its constructor throws `ArgumentException` when either value is not greater than zero or when `AccessCredentialLifetime` is greater than `SessionLifetime`.
- [ ] T013 Define the `internal` `SessionRejectionReason` enum with exactly `NotAuthenticated`, `MalformedCredential`, `CredentialExpired`, `SessionNotFound`, `SessionRevoked`, `SessionExpired`, `InactiveUser`, `InactiveApplication`, `InactiveMembership`, `ApplicationMismatch` in `src/GaussAuth.Application/Sessions/sessionRejectionReason.enum.cs`.
- [ ] T014 Define `SessionOperationResult` in `src/GaussAuth.Application/Sessions/sessionOperationResult.result.cs` following the `LoginOperationResult` pattern: public `IsSuccess`, `SessionId`, `UserId`, `ApplicationId`, `AccessCredential` (nullable string), `AccessCredentialExpiresAt` (nullable), `SessionExpiresAt` (nullable); `internal` `Reason` (`SessionRejectionReason?`); `Success(...)` and `Failure(SessionRejectionReason reason, ...)` factories; a failure exposes no externally visible detail.

### Security events

- [ ] T015 Add the values `SessionCreated`, `AccessRenewed`, `SessionRevoked`, `LogoutCompleted`, `AccessRejectedExpired`, `AccessRejectedRevoked`, `AccessRejectedInvalidState`, `AccessRejectedApplicationMismatch` to `SecurityEventType` in `src/GaussAuth.Application/Security/securityEventType.enum.cs` (keep the existing three values).
- [ ] T016 Change `ISecurityEventRecorder.RecordAsync` in `src/GaussAuth.Application/Security/Ports/securityEventRecorder.interface.cs` to `(SecurityEventType type, Guid? userId, Guid? applicationId, Guid? sessionId, CancellationToken cancellationToken)` and update the three existing `RecordAsync` calls in `src/GaussAuth.Application/Login/loginService.service.cs` to pass `null` for `sessionId`.
- [ ] T017 Update `LoggingSecurityEventRecorder` in `src/GaussAuth.Infrastructure/Security/loggingSecurityEventRecorder.service.cs` to the new signature and include `{SessionId}` in the structured log template (identifiers and event type only).
- [ ] T018 Update the `RecordingSecurityEventRecorder` test double in `tests/GaussAuth.Foundation.Tests/authenticationLoginTests.test.cs` to the new signature, keeping its existing assertions valid, and run `dotnet build GaussAuth.slnx` in the `sdk` container to confirm the solution compiles.

### Persistence

- [ ] T019 Add `public DbSet<Session> Sessions => Set<Session>();` and the `Session` mapping to `src/GaussAuth.Infrastructure/Persistence/authenticationDbContext.context.cs` matching `data-model.md` exactly: table `Sessions`; key `Id` (`ValueGeneratedNever`, constraint `PK_Sessions`); required `UserId` (`uuid`), `ApplicationId` (`uuid`), `CreatedAt` and `ExpiresAt` (`timestamp with time zone`); nullable `RevokedAt` (`timestamp with time zone`); foreign key from `(UserId, ApplicationId)` to the existing alternate key `AK_ApplicationMemberships_UserId_ApplicationId` with `DeleteBehavior.Restrict` (the same pattern as `UserRoles`) named `FK_Sessions_ApplicationMemberships_UserId_ApplicationId`; index `IX_Sessions_UserId_ApplicationId` on `(UserId, ApplicationId)`. No other table changes and no credential or token column.
- [ ] T020 Generate the migration in the `sdk` container with `dotnet ef migrations add AddSessions --project src/GaussAuth.Infrastructure --startup-project src/GaussAuth.Api --output-dir Persistence/Migrations`; rename the generated files to the repository's naming (`addSessions.migration.cs`, `addSessions.designer.cs`, snapshot stays `authenticationDbContextModelSnapshot.snapshot.cs`) keeping the class and `[Migration]` attribute names untouched; review the generated `Up`/`Down` to confirm it creates only the `Sessions` table, the foreign key, and the index; then apply it with `dotnet ef database update` in the `sdk` container against the existing PostgreSQL service.
- [ ] T021 Implement `SessionRepository` (`AddAsync`, `GetByIdAsync` using `SingleOrDefaultAsync` on `Id`, `SaveChangesAsync`) over `AuthenticationDbContext` in `src/GaussAuth.Infrastructure/Persistence/sessionRepository.repository.cs`.

### Signed access credential adapter

- [ ] T022 Implement `AccessCredentialSigningKey` (also implements `IAccessCredentialKeySet`) in `src/GaussAuth.Infrastructure/Sessions/accessCredentialSigningKey.service.cs`: a static factory that reads `Sessions:Signing:PrivateKeyPem` (PEM text) or `Sessions:Signing:PrivateKeyPemFile` (path), imports it with `ECDsa.ImportFromPem`, and requires curve `nistP256`; treats a null/whitespace value as missing; when no key is configured and the environment is Development, generates an ephemeral `ECDsa.Create(ECCurve.NamedCurves.nistP256)` key and logs a warning without key data; otherwise throws `InvalidOperationException("Access credential signing configuration is missing or invalid.")` with no key material or file path in the message; exposes an `ECDsaSecurityKey` whose `KeyId` is the RFC 7638 JWK thumbprint of the public key, and `GetPublicKeys()` returning one `PublicSigningKey` (`kty` `EC`, `crv` `P-256`, `use` `sig`, `alg` `ES256`, `kid`, base64url `x` and `y`) with no private parameter.
- [ ] T023 Implement `SignedAccessCredentialIssuer` (`IAccessCredentialIssuer`) in `src/GaussAuth.Infrastructure/Sessions/signedAccessCredentialIssuer.service.cs` using `JsonWebTokenHandler`: header `alg` `ES256`, `typ` `JWT`, `kid`; claims exactly `iss` (configured issuer from `Sessions:Issuer`, default `gaussauth`), `sub` (`UserId`), `aud` (`ApplicationId`), `sid` (`SessionId`), `iat`, `exp` taken from `AccessCredentialClaims`; no other claim; no `nbf`, no roles, no permissions, no email.
- [ ] T024 Implement `SignedAccessCredentialValidator` (`IAccessCredentialValidator`) in `src/GaussAuth.Infrastructure/Sessions/signedAccessCredentialValidator.service.cs` using `await JsonWebTokenHandler.ValidateTokenAsync(...)` with `RequireSignedTokens = true`, `ValidAlgorithms = ["ES256"]`, `IssuerSigningKey` from the signing key, `ValidIssuer` from `Sessions:Issuer`, `ValidateIssuer = true`, `ValidateAudience = false` (the audience is compared by the Application layer), `ValidateLifetime = false` (lifetime is judged by `SessionService` against `TimeProvider`), `RequireExpirationTime = true`; return `null` for any validation failure, any missing `sub`/`aud`/`sid`/`iat`/`exp`, or any claim that does not parse as the expected GUID/number; never throw for a bad credential and never log the credential.

### Composition and configuration

- [ ] T025 In `src/GaussAuth.Infrastructure/DependencyInjection/infrastructureServiceCollectionExtensions.extension.cs`: add an optional `IHostEnvironment? environment = null` parameter to `AddInfrastructure`; read `Sessions:SessionLifetimeMinutes` (default `480`) and `Sessions:AccessTokenLifetimeMinutes` (default `15`), build the `SessionPolicy` and register it as a singleton, converting `ArgumentException` into `InvalidOperationException("Session lifetime configuration is missing or invalid.")` with no values in the message (eager, like the existing connection check); create `AccessCredentialSigningKey` eagerly with the environment and register it as a singleton for both its concrete type and `IAccessCredentialKeySet`; register `ISessionRepository` → `SessionRepository`, `IAccessCredentialIssuer` → `SignedAccessCredentialIssuer`, `IAccessCredentialValidator` → `SignedAccessCredentialValidator` (issuer name from `Sessions:Issuer`).
- [ ] T026 Pass `builder.Environment` to `AddInfrastructure` in `src/GaussAuth.Api/Program.cs` (composition root only; no other change yet).
- [ ] T027 Add to `compose.dev.yml` (`sdk` service environment) `Sessions__Signing__PrivateKeyPem: ${SESSIONS_SIGNING_KEY_PEM:-}` and add a documented, empty `SESSIONS_SIGNING_KEY_PEM=` placeholder line (optional, never a real key) to `.env.example`.

### Test infrastructure

- [ ] T028 Add `MutableTimeProvider : TimeProvider` (settable current UTC time, `Advance(TimeSpan)`) in `tests/GaussAuth.Foundation.Tests/mutableTimeProvider.provider.cs`.
- [ ] T029 Add `"Microsoft.IdentityModel"` and `"System.IdentityModel"` to the `forbidden` assembly-reference prefixes checked for `GaussAuth.Domain` and `GaussAuth.Application` in `tests/GaussAuth.Foundation.Tests/architectureTests.test.cs`, leaving the package-reference assertions unchanged (SC-011).
- [ ] T030 Update the existing schema tests for the new migration and extend them: in both `tests/GaussAuth.Foundation.Tests/migrationTests.test.cs` and `tests/GaussAuth.Foundation.Tests/usersSchemaMigrationTests.test.cs` change the hard-coded applied-migration count from `4` to `5` and add `"Sessions"` to `expectedTables`; in `usersSchemaMigrationTests.test.cs` remove the `forbiddenNamePatterns = { "Session" }` assertion (a `002-users-profiles` scope guard that this feature intentionally supersedes) and add `Sessions` to the foreign-key table list if that list is meant to cover all membership-dependent tables; and in `migrationTests.test.cs` additionally assert that the `Sessions` table has the columns and types in `data-model.md`, the foreign key `FK_Sessions_ApplicationMemberships_UserId_ApplicationId` exists, and index `IX_Sessions_UserId_ApplicationId` exists.
- [ ] T031 Extend `tests/GaussAuth.Foundation.Tests/startupTests.test.cs`, following its existing configuration-failure pattern, with tests that startup fails with a generic message (no configured values, no key text) when `Sessions:SessionLifetimeMinutes` or `Sessions:AccessTokenLifetimeMinutes` is zero or negative, when the access lifetime exceeds the session lifetime, and when no signing key is configured outside Development (follow the existing `Invalid_database_configuration_fails_without_echoing_its_value` pattern: start the built API assembly as a `dotnet` child process with `ASPNETCORE_ENVIRONMENT=Production`, a syntactically valid `ConnectionStrings__AuthenticationDatabase`, and the offending `Sessions__*` setting, assert a non-zero exit and the generic message, and assert the configured values or key text never appear in the output); and that Development without a key starts successfully (ephemeral key).

**Checkpoint**: The solution builds; the `Sessions` table exists; the token adapter, key handling, ports, and configuration are in place; Domain and Application reference no token or identity-model assembly, enforced by test.

---

## Phase 3: User Story 1 - Establish a Session After Successful Login (Priority: P1) 🎯 MVP

**Goal**: A successful login creates a session for exactly that user and application and returns a usable access credential in the same interaction.

**Independent Test**: Log in as an active user with an active membership in an active application; confirm a `Sessions` row exists for that user and application with creation and expiration times, and the response carries a credential whose claims are limited to the six defined names.

### Tests for User Story 1

- [ ] T032 [US1] Create `tests/GaussAuth.Foundation.Tests/sessionDomainTests.test.cs` with domain unit tests: `Session.Create` sets `ExpiresAt = now + lifetime`; empty `id`/`userId`/`applicationId` and a zero or negative lifetime are rejected; `GetState` returns `Active` before expiry, `Expired` at and after `ExpiresAt`, and `Revoked` when `RevokedAt` is set even if also past expiry.
- [ ] T033 [US1] Create `tests/GaussAuth.Foundation.Tests/sessionsAccessTests.test.cs` with the shared helpers modeled on `authenticationLoginTests.test.cs`: a `FactoryAsync` that clears and sets its own `ManagedEnvironmentKeys` list (`RateLimiting__Login__PermitLimit` set high, e.g. `1000`, plus `Sessions__SessionLifetimeMinutes`, `Sessions__AccessTokenLifetimeMinutes`, `Sessions__Issuer`, and the `RateLimiting__SigningKeys__*` and `RateLimiting__SessionCredentials__*` names so tests reset them) and registers a `MutableTimeProvider` singleton through `ConfigureServices`; `CreateUserAsync`, `CreateApplicationAsync`, `CreateMembershipAsync`; and a `LoginAsync` helper returning the parsed `sessionId`, `accessToken`, `expiresAt`, and `sessionExpiresAt`. Every test double used by this class (throwing `ISessionRepository`, capturing `ILoggerProvider`, recording `ISecurityEventRecorder`) is declared as a `private sealed` nested class, following `RecordingSecurityEventRecorder`, so the architecture test's one-top-level-type-per-file rule still holds.
- [ ] T034 [US1] Add integration test in `tests/GaussAuth.Foundation.Tests/sessionsAccessTests.test.cs`: a successful login returns `200` with `sessionId`, `tokenType` `Bearer`, `accessToken`, `expiresAt`, `sessionExpiresAt`, `Cache-Control: no-store`, and unchanged `userId`/`applicationId`; a `Sessions` row read through a fresh scope has that id, the correct `UserId` and `ApplicationId`, `RevokedAt` null, and `ExpiresAt` equal to `CreatedAt` plus 8 hours; and a login request body that also carries extra `userId`, `applicationId`, or `sessionId` fields still produces a session only for the authenticated user and application (client-supplied identity is ignored).
- [ ] T035 [US1] Add integration test: the credential's payload (decoded without verifying) contains exactly the claim names `iss`, `sub`, `aud`, `sid`, `iat`, `exp` with `sub`/`aud`/`sid` equal to the user, application, and session ids and `exp` not later than `sessionExpiresAt`; the credential contains no email, password text, or role/permission claim.

### Implementation for User Story 1

- [ ] T036 [US1] Change `LoginOperationResult.Success(...)` from `public` to `internal` in `src/GaussAuth.Application/Login/loginOperationResult.result.cs` so only code in the Application assembly can mint a successful authentication result (research R8); build in the `sdk` container to confirm nothing outside the assembly used it.
- [ ] T037 [US1] Create `SessionService` in `src/GaussAuth.Application/Sessions/sessionService.service.cs` with constructor dependencies `ISessionRepository`, `IUserRepository`, `IApplicationRepository`, `IApplicationMembershipRepository`, `IAccessCredentialIssuer`, `IAccessCredentialValidator`, `SessionPolicy`, `TimeProvider`, and `ILogger<SessionService>`; implement `CreateAsync(LoginOperationResult authentication, CancellationToken cancellationToken)`: reject (`NotAuthenticated`) a non-success result; through a private `EvaluateEligibilityAsync(Guid userId, Guid applicationId, CancellationToken)` re-verify that the user is active (`InactiveUser`), the application is active (`InactiveApplication`), and the membership exists and is active (`InactiveMembership`) without touching any password; create `Session.Create(Guid.NewGuid(), userId, applicationId, now, policy.SessionLifetime)` with `now` from `TimeProvider.GetUtcNow()`; compute `exp = min(now + policy.AccessCredentialLifetime, session.ExpiresAt)`; issue the credential **before** saving; add and save the session; return `SessionOperationResult.Success` carrying the identifiers, credential, credential expiry, and session expiry. Rejections return `SessionOperationResult.Failure(reason)` only (no logging or events yet; User Story 6 adds them), and the private eligibility method is the single shared implementation reused by validate and renew.
- [ ] T038 [US1] Register `SessionService` as scoped and `TimeProvider.System` as a singleton (`TryAddSingleton`, so tests can replace it) in `src/GaussAuth.Api/DependencyInjection/applicationServiceCollectionExtensions.extension.cs`.
- [ ] T039 [US1] Extend `LoginResponse` with `Guid SessionId`, `string TokenType`, `string AccessToken`, `DateTimeOffset ExpiresAt`, and `DateTimeOffset SessionExpiresAt` (after the existing `UserId` and `ApplicationId`) in `src/GaussAuth.Api/Login/loginResponse.dto.cs`.
- [ ] T040 [US1] Update `LoginAsync` in `src/GaussAuth.Api/Login/loginEndpoints.extension.cs` to inject `SessionService`, call `AuthenticateAsync`, return the existing uniform `401` when authentication fails, then call `SessionService.CreateAsync(result, cancellationToken)` and return the same uniform `401` if it fails, otherwise return `200` with the extended `LoginResponse` (`TokenType` `Bearer`) and set `Cache-Control: no-store` on the response; request validation, route, and rate limiting stay unchanged. Run the full test suite in the `sdk` container and confirm every existing 005 test still passes.

### Verification for User Story 1 (requires the service from the preceding implementation tasks)

- [ ] T041 [US1] Add integration test: resolve `LoginService` and `SessionService` from `factory.Services` in a scope, authenticate successfully, then deactivate the membership through the existing API, then call `SessionService.CreateAsync` with the earlier `LoginOperationResult`; assert the result is a failure and no `Sessions` row exists for that user and application (FR-004: no stale authentication context creates a session).

**Checkpoint**: Login creates and persists a session and returns a credential; User Story 1 is independently verifiable and is the MVP.

---

## Phase 4: User Story 2 - Validate Access Credentials (Priority: P1)

**Goal**: A consuming application can verify a credential locally with the published public key and obtain an authoritative answer, including renewal, with uniform rejection of every invalid credential.

**Independent Test**: Validate a fresh credential and confirm the identity context; then validate tampered, wrong-algorithm, expired, and wrong-application credentials and confirm identical `401` responses; renew a valid credential and confirm the bounded expiry.

### Tests for User Story 2

- [ ] T042 [US2] Add integration test in `tests/GaussAuth.Foundation.Tests/sessionsAccessTests.test.cs`: `POST /auth/session/validate` with a fresh credential and the correct `applicationCode` returns `200` with the matching `userId`, `applicationId`, `sessionId`, and `expiresAt`.
- [ ] T043 [US2] Add integration test: a credential with one altered character, a credential whose header is rewritten to `alg: none` with an empty signature, a credential signed by an unrelated ES256 key, a malformed string, and a missing or non-`Bearer` `Authorization` header each return `401` with a response body and `WWW-Authenticate: Bearer` identical to every other rejection.
- [ ] T044 [US2] Add integration test: after advancing `MutableTimeProvider` past `expiresAt` (but before `sessionExpiresAt`) validate and renew return `401`; a request with a valid credential and an invalid body (missing or over-64-character `applicationCode`) returns `400` and never echoes the credential; and with `ISessionRepository` replaced (through `ConfigureServices`) by a test double that throws, `validate` returns `500` Problem Details titled "An unexpected error occurred." with no exception text, SQL, or stack trace, and never `200` (FR-021).
- [ ] T045 [US2] Add integration test: `POST /auth/session/renew` with a valid credential returns `200` with the same `sessionId`, a new `accessToken`, an `expiresAt` equal to the lesser of now plus 15 minutes and `sessionExpiresAt`, an unchanged `sessionExpiresAt`, and `Cache-Control: no-store`; renewing near the end of the session (advance the clock to within 5 minutes of `sessionExpiresAt`) caps `expiresAt` at `sessionExpiresAt`.
- [ ] T046 [US2] Add integration test: `GET /auth/signing-keys` returns `200` with a JSON key set containing one key with `kty` `EC`, `crv` `P-256`, `use` `sig`, `alg` `ES256`, a `kid` equal to the credential header's `kid`, `x` and `y`, and no `d` or any private member, with `Cache-Control: public, max-age=300`; and that a credential verifies against that published key using `ECDsa` imported from the returned `x`/`y`; and that exceeding `RateLimiting__SigningKeys__PermitLimit` (set low for the test) returns `429`, and exceeding `RateLimiting__SessionCredentials__PermitLimit` (set low for the test) on `validate` returns `429` while the default factory (limit 600) never throttles the other tests in this class.

### Implementation for User Story 2

- [ ] T047 [US2] Add `ValidateAsync(string credential, string applicationCode, CancellationToken)` to `src/GaussAuth.Application/Sessions/sessionService.service.cs` implementing the evaluation order in `data-model.md`: `IAccessCredentialValidator.ValidateAsync` returns `null` → `MalformedCredential`; credential `ExpiresAt <= now` (from `TimeProvider`) → `CredentialExpired`; session not found → `SessionNotFound`; token `sid`/`sub`/`aud` differ from the session's id/user/application → `ApplicationMismatch`; the code resolved through `IApplicationRepository.GetByCodeAsync` (trimmed and lower-cased like `ApplicationService`) is not the session's application → `ApplicationMismatch`; `GetState(now)` is `Revoked` → `SessionRevoked`; `Expired` → `SessionExpired`; then the shared `EvaluateEligibilityAsync`; on success return `Success` with `SessionId`, `UserId`, `ApplicationId`, and the credential's `ExpiresAt` as `AccessCredentialExpiresAt`.
- [ ] T048 [US2] Add `RenewAsync(string credential, CancellationToken)` to the same file: perform the same checks as validation without an application-code comparison, then issue a new credential for the same session with `exp = min(now + policy.AccessCredentialLifetime, session.ExpiresAt)`, never changing the session; return `Success` with the new credential, its expiry, and the unchanged session expiry.
- [ ] T049 [US2] Add the `HttpRequest` extension `ReadBearerCredential()` in `src/GaussAuth.Api/Sessions/bearerCredentialReader.extension.cs` returning the credential string or `null` when the `Authorization` header is absent, is not the `Bearer` scheme (case-insensitive), or the value is empty or longer than 4096 characters.
- [ ] T050 [US2] Create the DTOs in `src/GaussAuth.Api/Sessions/`: `ValidateSessionRequest` (`ApplicationCode`: required, maximum 64 characters) in `validateSessionRequest.dto.cs`; `AccessCredentialResponse` (`Guid SessionId`, `string TokenType`, `string AccessToken`, `DateTimeOffset ExpiresAt`, `DateTimeOffset SessionExpiresAt`) in `accessCredentialResponse.dto.cs`; `SessionContextResponse` (`Guid UserId`, `Guid ApplicationId`, `Guid SessionId`, `DateTimeOffset ExpiresAt`) in `sessionContextResponse.dto.cs`; `SigningKeyResponse` (`kty`, `crv`, `use`, `alg`, `kid`, `x`, `y`, serialized with those lowercase names) in `signingKeyResponse.dto.cs`; and `SigningKeySetResponse` (`keys`) in `signingKeySetResponse.dto.cs`.
- [ ] T051 [US2] Create `MapSessionsEndpoints` in `src/GaussAuth.Api/Sessions/sessionsEndpoints.extension.cs` with `POST /auth/session/validate`, `POST /auth/session/renew`, and `GET /auth/signing-keys` per `contracts/sessions-api.md`: a missing or malformed bearer header returns `401` before body validation; an invalid `ValidateSessionRequest` returns `400` validation problem (reuse the `HasValidationErrors` approach from `loginEndpoints.extension.cs`); every credential rejection returns `TypedResults.Problem(statusCode: 401, title: "Access is not valid.")` with `WWW-Authenticate: Bearer`; renew sets `Cache-Control: no-store`; the keys route maps `IAccessCredentialKeySet.GetPublicKeys()` into `SigningKeySetResponse`, sets `Cache-Control: public, max-age=300`, and applies `.RequireRateLimiting("signing-keys")`; `validate` and `renew` apply `.RequireRateLimiting("session-credentials")`.
- [ ] T052 [US2] Add two fixed-window rate-limiting policies to `src/GaussAuth.Api/DependencyInjection/apiServiceCollectionExtensions.extension.cs`, partitioned by remote IP like the existing policies: `signing-keys` reading `RateLimiting:SigningKeys:PermitLimit` (default `60`) and `RateLimiting:SigningKeys:WindowSeconds` (default `60`), and `session-credentials` reading `RateLimiting:SessionCredentials:PermitLimit` (default `600`) and `RateLimiting:SessionCredentials:WindowSeconds` (default `60`).
- [ ] T053 [US2] Call `app.MapSessionsEndpoints();` in `src/GaussAuth.Api/Program.cs` after `MapLoginEndpoints()`. Run the full test suite in the `sdk` container.

**Checkpoint**: Credentials can be validated, renewed, and verified locally; every invalid credential receives the same safe response.

---

## Phase 5: User Story 3 - Log Out and Revoke a Session (Priority: P1)

**Goal**: Logout durably revokes the session so its credentials stop working before their expiry, idempotently and without leaking state.

**Independent Test**: Create a session, confirm validation succeeds, log out, confirm validation and renewal now fail and the persisted `RevokedAt` is set, and confirm a repeated logout is safe.

### Tests for User Story 3

- [ ] T054 [US3] Add domain unit tests in `tests/GaussAuth.Foundation.Tests/sessionDomainTests.test.cs`: `Revoke` sets `RevokedAt`; a second `Revoke` with a later time leaves the first `RevokedAt` unchanged; revoking an expired session reports `Revoked`.
- [ ] T055 [US3] Add integration test in `tests/GaussAuth.Foundation.Tests/sessionsAccessTests.test.cs`: after `POST /auth/logout` (`204`), validate and renew with the same credential return `401` although it has not reached `exp`, and `RevokedAt` is set on the `Sessions` row when read through a fresh scope from a new `DbContext` (durability).
- [ ] T056 [US3] Add integration test: logout is idempotent (a second logout returns `204`); logout with a signature-valid credential whose token has expired (clock advanced past `expiresAt`) returns `204` and revokes the still-active session; logout for a credential whose session row was removed returns `204`; logout with a forged, malformed, or missing credential returns the uniform `401`.

### Implementation for User Story 3

- [ ] T057 [US3] Add to `src/GaussAuth.Application/Sessions/sessionService.service.cs` a public `RevokeAsync(Guid sessionId, CancellationToken)` that revokes one specific session (FR-013): load the session; if it exists and `RevokedAt` is `null`, call `Revoke(now)` and `SaveChangesAsync`; return `Success` in every other case (session unknown, already revoked, or already expired) so the operation is idempotent and non-revealing; no bulk or per-user revocation is added. Then add `LogoutAsync(string credential, CancellationToken)`: call `IAccessCredentialValidator.ValidateAsync` and return `Failure(MalformedCredential)` when `null`; ignore token expiry; delegate to `RevokeAsync(claims.SessionId, ...)` and return its result.
- [ ] T058 [US3] Add `POST /auth/logout` to `src/GaussAuth.Api/Sessions/sessionsEndpoints.extension.cs`: `204` on success, the uniform `401` (`WWW-Authenticate: Bearer`) otherwise; no request body; apply `.RequireRateLimiting("session-credentials")`. Run the full test suite in the `sdk` container.

**Checkpoint**: Logout works, is durable, and is safe to repeat.

---

## Phase 6: User Story 4 - Application Isolation and Independent Concurrent Sessions (Priority: P1)

**Goal**: Every session belongs to exactly one application and can be used, expired, and revoked independently of the user's other sessions.

**Independent Test**: Give one user two sessions in application X and one in application Y; confirm all three work independently, revoking one leaves the others valid, and the application Y session is rejected in application X's context even though the user is a member of both.

### Tests for User Story 4

- [ ] T059 [US4] Add integration test in `tests/GaussAuth.Foundation.Tests/sessionsAccessTests.test.cs`: three logins (two for application X, one for application Y) return three distinct `sessionId` values, each validates to its own session and application, and logging in again never replaces or revokes an earlier session.
- [ ] T060 [US4] Add integration test: logging out one session, and separately revoking one specific session through `SessionService.RevokeAsync(sessionId)` resolved from `factory.Services`, each leave the user's other two sessions valid, and advancing the clock past a short-lived session's expiry (configure `Sessions__SessionLifetimeMinutes` and `Sessions__AccessTokenLifetimeMinutes` low for this factory) does not affect a session created later.
- [ ] T061 [US4] Add integration test: a user with active memberships in applications A and B and a credential issued for A is rejected by `validate` when the request names application B's code, and accepted for A's code; both rejections are identical to any other `401`.

### Implementation for User Story 4

No production code is scheduled for this story: the composite membership key, the `aud`/session/caller-application comparisons in `ValidateAsync`, and the insert-only session creation already produced by Phases 2-4 satisfy it. If any test above fails, correct only the responsible `SessionService`, mapping, or endpoint code.

- [ ] T062 [US4] Confirm no single-session restriction exists anywhere in `SessionService` or `SessionRepository` and no uniqueness constraint other than `PK_Sessions` and the foreign key was introduced on `Sessions`, by searching both for session-count limits and checking the generated migration; if any is found, remove it.

**Checkpoint**: Isolation and independence hold in every tested scenario.

---

## Phase 7: User Story 5 - Reflect Loss of Eligibility in Existing Sessions (Priority: P2)

**Goal**: Deactivating a user, application, or membership blocks existing sessions at the next authoritative check without revoking them, and reactivation restores access if the session is neither expired nor revoked; new sessions are refused while ineligible.

**Independent Test**: Create a session, deactivate each of user, application, and membership in turn, confirm validation and renewal fail while inactive and succeed again after reactivation, and confirm login creates no session while inactive.

### Tests for User Story 5

- [ ] T063 [US5] Add integration test in `tests/GaussAuth.Foundation.Tests/sessionsAccessTests.test.cs`: for each of user, application, and membership, deactivating it through the existing API makes `validate` and `renew` return `401` immediately for the existing session, and no new `Sessions` row is created by a login attempt (the existing uniform `401` is returned).
- [ ] T064 [US5] Add integration test: after reactivating each entity (existing activate routes) the same still-unexpired, non-revoked credential validates again with `200`, and the session row's `RevokedAt` remained null throughout; a session that was revoked before reactivation stays rejected.
- [ ] T065 [US5] Add integration test using the existing `004-roles-permissions` routes: create a role and permission in the application, assign the permission to the role and the role to the user; `GET /applications/{applicationId}/users/{userId}/effective-permissions` lists the permission; then deactivate the role, and separately deactivate the permission and the membership, each time confirming the effective-permissions lookup no longer lists it (and never lists it for a second application) at the next lookup, while the same credential's decoded claim names stay exactly `iss`, `sub`, `aud`, `sid`, `iat`, `exp` and the credential still validates until the session's own state changes (FR-019, SC-008).

### Implementation for User Story 5

No production code is scheduled for this story: eligibility is evaluated by the single shared `EvaluateEligibilityAsync` in `SessionService` (T037), reused by creation, validation, and renewal, and deactivation features (002/003) are intentionally unchanged. If a test fails, correct only the eligibility evaluation.

- [ ] T066 [US5] Confirm that `SessionService` never calls any `Deactivate`, `Revoke`, or write operation as a result of an eligibility failure (eligibility loss only refuses access) and that `EffectivePermissionService` and the `004-roles-permissions` files are unmodified (`git diff` is empty for them).

**Checkpoint**: Eligibility loss and regain behave exactly as clarified.

---

## Phase 8: User Story 6 - Observe Session Lifecycle Safely (Priority: P3)

**Goal**: Session creation, renewal, revocation, logout, and rejected access are logged and recorded as security events with identifiers only, never credentials.

**Independent Test**: Exercise create, renew, logout, and each rejection class; confirm the recorded events and captured logs carry only user, application, and session identifiers and event types, and that no credential text appears anywhere.

### Tests for User Story 6

- [ ] T067 [US6] Add integration test in `tests/GaussAuth.Foundation.Tests/sessionsAccessTests.test.cs` using a recording `ISecurityEventRecorder` (replace the registration as `authenticationLoginTests.test.cs` does) asserting events with correct user/application/session ids: `SessionCreated` on login, `AccessRenewed` on renew, `SessionRevoked` followed by `LogoutCompleted` on logout, `AccessRejectedRevoked` for a revoked session, `AccessRejectedExpired` for an expired credential, `AccessRejectedInvalidState` for a deactivated user, and `AccessRejectedApplicationMismatch` for a wrong application code.
- [ ] T068 [US6] Add integration test: a tampered or malformed credential produces no security event (only a Debug-level log), and across all scenarios in this class a capturing `ILoggerProvider` and the recording recorder never contain any substring of an issued access credential, the signing key text, or the password (FR-022, FR-024).

### Implementation for User Story 6

- [ ] T069 [US6] Add `ISecurityEventRecorder` to `SessionService`'s constructor in `src/GaussAuth.Application/Sessions/sessionService.service.cs` and route every failure through one private `RejectAsync(SessionRejectionReason reason, Guid? userId, Guid? applicationId, Guid? sessionId, CancellationToken)` that logs at Information with `{UserId} {ApplicationId} {SessionId} {Reason}` (Debug only and no event for `MalformedCredential`, `NotAuthenticated`) and records `AccessRejectedExpired` for `CredentialExpired`/`SessionExpired`, `AccessRejectedRevoked` for `SessionRevoked`, `AccessRejectedApplicationMismatch` for `ApplicationMismatch`, and `AccessRejectedInvalidState` for `SessionNotFound`/`InactiveUser`/`InactiveApplication`/`InactiveMembership`; never log or record the credential value. Rejections raised by `CreateAsync` (refused session creation) are logged at Information with the identifiers and reason but record no security event, because the preceding authentication already recorded its own login events in `005-authentication-login`.
- [ ] T070 [US6] Add the success-path logging and events to `SessionService`: `SessionCreated` after saving a new session, `AccessRenewed` after a renewal, and in `LogoutAsync` `SessionRevoked` emitted by `RevokeAsync` after it revokes an active session, followed by `LogoutCompleted` emitted by `LogoutAsync` for every signature-valid logout; each logs only identifiers and the event type. Run the full test suite in the `sdk` container.

**Checkpoint**: Session lifecycle is observable without exposing credentials; events are shaped for later audit consolidation (`009-security-audit`).

---

## Phase 9: Polish and Cross-Cutting Validation

**Purpose**: Validate architecture, safe contracts, scope boundaries, and end-to-end behavior for the completed feature.

- [ ] T071 Verify every new C# source file has exactly one top-level type and a `<name>.<type>.cs` filename (including `sessionState.enum.cs`, `session.entity.cs`, `accessCredentialClaims.result.cs`, `publicSigningKey.result.cs`, the four port interface files, `sessionPolicy.options.cs`, `sessionRejectionReason.enum.cs`, `sessionOperationResult.result.cs`, `sessionService.service.cs`, `mutableTimeProvider.provider.cs`, and each API DTO); confirm the `Microsoft.IdentityModel`/`System.IdentityModel` forbidden-reference check from T029 passes for `GaussAuth.Domain` and `GaussAuth.Application`, and that only `GaussAuth.Infrastructure.csproj` gained a package.
- [ ] T072 Review `src/GaussAuth.Domain/Sessions/`, `src/GaussAuth.Application/Sessions/`, `src/GaussAuth.Infrastructure/Sessions/`, and `src/GaussAuth.Api/Sessions/` for: only ES256 accepted, no `alg=none` path, exact claim set, `exp <= session expiry`, uniform `401` with no cause-revealing text, no credential or key in logs, responses, or exceptions, no refresh token, rotation, cache, OAuth 2.0 or OpenID Connect code, no password recovery, MFA, social login, or authorization-contract code (FR-028); that `SessionService.CreateAsync` references neither `ICredentialVerificationService` nor `ICredentialProvisioningService` (no password re-validation, FR-002); and that the `(UserId, ApplicationId)` index and the unchanged `ISessionRepository` shape leave a later per-user revocation and the `007`/`008` features free to build on the model without restructuring (SC-012).
- [ ] T073 Confirm the generated migration files and `.gitignore`/repository contain no signing key, PEM text, or secret, that `.env.example` and `compose.dev.yml` carry only the empty `SESSIONS_SIGNING_KEY_PEM` placeholder, and that the Domain and Application `.csproj` files have no diff against `main` (no new package or project reference).
- [ ] T074 Run the full Docker test suite (`dotnet test GaussAuth.slnx` in the `sdk` container) and the API scenarios from `specs/006-sessions-access/quickstart.md` (login, validate, isolation, keys, renewal, eligibility toggling, logout durability, restart persistence, and the log-hygiene grep); correct only feature-006 artifacts and failures found.

## Dependencies and Execution Order

- Phase 1 precedes Phase 2.
- Phase 2 blocks every user story: the Domain entity, ports, `SessionPolicy`, persistence, signing key, token adapter, configuration, and test infrastructure are shared.
- US1 (Phase 3) delivers session creation plus the credential and is the MVP; it also writes the shared `EvaluateEligibilityAsync` that later stories reuse.
- US2 (Phase 4) depends on US1 (it validates credentials that login issues).
- US3 (Phase 5) depends on US2 (validation proves revocation took effect).
- US4 (Phase 6) and US5 (Phase 7) verify behavior already produced by US1-US3 and add no production code; US5 depends on US2 for the validate/renew routes.
- US6 (Phase 8) adds logging and events to the completed `SessionService` and depends on US1-US3.
- Phase 9 follows all stories.

## Success Criteria Coverage

| Criterion | Verified by |
|---|---|
| SC-001 | T034, T040 |
| SC-002 | T032, T034 |
| SC-003 | T043, T044 |
| SC-004 | T055, T074 |
| SC-005 | T061 |
| SC-006 | T060 |
| SC-007 | T063 |
| SC-008 | T064, T065 |
| SC-009 | T035, T045 |
| SC-010 | T068 |
| SC-011 | T029, T071 |
| SC-012 | T072 |

## Parallel Opportunities

None are scheduled. `SessionService` is the single orchestration point every story's behavior depends on, and the project constitution requires one sequential implementation stream rather than parallel execution merely because some files differ.

## Implementation Strategy

1. Complete setup and the shared foundation: Domain entity, ports, migration, signing key, token adapter, configuration, and test infrastructure (Phases 1-2).
2. Deliver the MVP: login creates a persisted session and returns a signed credential (US1).
3. Add authoritative validation, renewal, and local-verification keys (US2), then durable logout (US3).
4. Verify application isolation, concurrency, and eligibility semantics (US4, US5), which require no new production code.
5. Add structured logging and security events (US6).
6. Validate architecture, safe contracts, scope boundaries, and the full Docker test suite and quickstart (Phase 9).

## Format Validation

All 74 tasks use the required checkbox, sequential `T###` identifier, story labels only in user-story phases (Phases 3-8), and explicit repository-relative file paths. No `[P]` markers are used because sequential execution is required by project governance. Clarified-decision review (T001): performed; no deviation (see T001).
