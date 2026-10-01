---

description: "Dependency-ordered implementation tasks for the 002-users-profiles feature"
---

# Tasks: Global User Identity and Profile

**Input**: Design documents from `specs/002-users-profiles/`

**Prerequisites**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md),
[data-model.md](data-model.md), [contracts/users-api.md](contracts/users-api.md),
and [quickstart.md](quickstart.md).

**Tests**: The specification's Testing Strategy explicitly requires essential
tests for creation, duplicate-email rejection, retrieval, profile update,
activation/deactivation, and persistence/migration. No coverage target or
broad synthetic suite is added. `ArchitectureTests` from `001-foundation`
already scans every file under `src/` and `tests/` generically, so no new
architecture test is written for this feature's Domain/Application files.

**Organization**: Tasks are grouped by the four user stories in
[spec.md](spec.md) (US1/US2 at P1, US3/US4 at P2). One well-contextualized
agent executes them sequentially. The constitution overrides template
suggestions to mark independent tasks for parallel work; no `[P]` marker is
used in this feature, matching `001-foundation`.

## Format: `[ID] [Story] Description`

Every task uses `- [ ] TNNN`; story-phase tasks also carry `[US1]`, `[US2]`,
`[US3]`, or `[US4]`. Every task names the files it creates or edits. No `[P]`
marker is used because the constitution prohibits encouraging parallel
agents merely because files differ.

## Path Conventions

Paths are relative to the repository root. `src/GaussAuth.Api/Program.cs`
remains the composition root with no declared top-level type; all other C#
files use `<name>.<type>.cs` and exactly one top-level type. The EF-generated
migration files for this feature must be renamed to that convention without
changing migration IDs or generated class identities, exactly as
`001-foundation` did for its initial migration.

## Phase 1: Setup

**Purpose**: Confirm no new dependency is required before starting.

- [ ] T001 Verify no new NuGet package is required for this feature: confirm
  `Microsoft.AspNetCore.Identity.ILookupNormalizer` already resolves from the
  `AddIdentityCore` registration in
  `src/GaussAuth.Infrastructure/DependencyInjection/infrastructureServiceCollectionExtensions.extension.cs`;
  confirm `Microsoft.AspNetCore.RateLimiting` (the fixed-window rate limiter
  used in US1) ships in the ASP.NET Core shared framework already referenced
  by `src/GaussAuth.Api/GaussAuth.Api.csproj` (`Microsoft.NET.Sdk.Web`); and
  confirm `tests/GaussAuth.Foundation.Tests/GaussAuth.Foundation.Tests.csproj`
  already references all four production assemblies plus MSTest and
  `Microsoft.AspNetCore.Mvc.Testing` (per `research.md`'s "Test project
  placement" and "Email normalization" decisions). Do not add any package
  reference.

**Checkpoint**: Confirmed the existing solution and test project already
provide everything this feature needs.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Domain, ports, persistence mapping, migration, and the shared
response contract that every user story depends on.

**⚠️ CRITICAL**: No user story task may start until this phase is complete.

- [ ] T002 Create `src/GaussAuth.Domain/Users/user.entity.cs` with a `User`
  aggregate root (no EF Core/ASP.NET Core/Identity reference): `Id` (`Guid`,
  stable, set once), `Email` (`string`, required, max 256 chars, immutable
  after creation), `NormalizedEmail` (`string`, unique at persistence),
  `IsActive` (`bool`, `true` by default on creation), `CreatedAt`/`UpdatedAt`
  (`DateTimeOffset`), and an owned `Profile` (`UserProfile`, never null). Add
  a `Create(id, email, normalizedEmail, profile, now)` factory and
  `Activate(now)`/`Deactivate(now)` methods that are idempotent — calling
  either while already in that state is a no-op that still succeeds and
  does **not** advance `UpdatedAt` (per spec Clarifications 2026-10-01 and
  `data-model.md`'s state-transition diagram).
- [ ] T003 Create `src/GaussAuth.Domain/Users/userProfile.entity.cs` with a
  `UserProfile` owned entity (no EF Core/ASP.NET Core/Identity reference):
  `UserId` (`Guid`, same value as the owning `User.Id`), `FirstName`
  (`string`, required, max 100 chars), `LastName` (`string`, required, max
  100 chars), `DisplayName` (`string`, required, max 100 chars),
  `PhoneNumber` (`string?`, optional, max 32 chars, no uniqueness or
  carrier-specific normalization), `AvatarReference` (`string?`, optional,
  opaque value, max 2048 chars, not validated against an actual
  file/resource), and `CreatedAt`/`UpdatedAt` (`DateTimeOffset`). Add an
  `UpdateProfile(firstName, lastName, displayName, phoneNumber,
  avatarReference, now)` method that advances `UpdatedAt` and never touches
  identity/credential fields.
- [ ] T004 Create
  `src/GaussAuth.Application/Users/Ports/userRepository.interface.cs` with
  `IUserRepository`: `Task<bool> ExistsByNormalizedEmailAsync(string
  normalizedEmail, CancellationToken ct)`, `Task AddAsync(User user,
  CancellationToken ct)`, `Task<User?> GetByIdAsync(Guid id,
  CancellationToken ct)`, `Task SaveChangesAsync(CancellationToken ct)`.
  Application MUST NOT depend on a concrete persistence implementation.
- [ ] T005 Create
  `src/GaussAuth.Application/Users/Ports/credentialProvisioningService.interface.cs`
  with `ICredentialProvisioningService`: `string NormalizeEmail(string
  email)` and `Task<CredentialProvisioningResult> CreateCredentialAsync(Guid
  userId, string email, string password, CancellationToken ct)` (define
  `CredentialProvisioningResult` as a small success/failure outcome type in
  the same file or an adjacent file, not a generic result framework). This
  port MUST be the only way Application triggers Identity credential
  creation; it MUST NOT reference `UserManager` or any other concrete
  Identity type.
- [ ] T006 Extend
  `src/GaussAuth.Infrastructure/Persistence/authenticationDbContext.context.cs`
  per `research.md`'s "EF Core naming collision to avoid" and
  `data-model.md`'s "Persistence mapping" section: add `DbSet<User>
  DomainUsers` mapped to table `Users` (NOT named `Users` as a property,
  since `IdentityUserContext<IdentityUser<Guid>, Guid>` already exposes an
  inherited `DbSet<IdentityUser<Guid>> Users` mapped to `AspNetUsers`) and
  `DbSet<UserProfile> UserProfiles` mapped to table `UserProfiles`. In
  `OnModelCreating`, configure: `Users.Id` as a non-generated
  (`ValueGeneratedNever`) primary key that is also a one-to-one foreign key
  to `AspNetUsers.Id` with `DeleteBehavior.Cascade`; `UserProfiles.UserId` as
  primary key and one-to-one foreign key to `Users.Id` with
  `DeleteBehavior.Cascade`; and a unique index on `Users.NormalizedEmail`.
- [ ] T007 Create
  `src/GaussAuth.Infrastructure/Persistence/userRepository.repository.cs`
  implementing `IUserRepository` against `AuthenticationDbContext`
  (`DomainUsers`/`UserProfiles` DbSets added in T006), including eager
  loading of the owned `Profile` on `GetByIdAsync`.
- [ ] T008 Create
  `src/GaussAuth.Infrastructure/Identity/identityCredentialProvisioningService.service.cs`
  implementing `ICredentialProvisioningService`: `NormalizeEmail` delegates
  to the injected `ILookupNormalizer` (the same normalizer
  `AddIdentityCore`/`UserManager` already use internally, per `research.md`'s
  "Email normalization" decision); `CreateCredentialAsync` constructs an
  `IdentityUser<Guid>` with the caller-supplied `userId` and submitted
  email/normalized email, then calls
  `UserManager<IdentityUser<Guid>>.CreateAsync(identityUser, password)` and
  translates Identity's `IdentityResult` (including password-policy
  failures) into `CredentialProvisioningResult`, never logging or returning
  the password or any password hash.
- [ ] T009 Extend
  `src/GaussAuth.Infrastructure/DependencyInjection/infrastructureServiceCollectionExtensions.extension.cs`
  to register `IUserRepository → UserRepository` and
  `ICredentialProvisioningService → IdentityCredentialProvisioningService`
  (both scoped, consistent with the existing `AddDbContext`/
  `AddIdentityCore` lifetimes in that file).
- [ ] T010 Using `.config/dotnet-tools.json`'s pinned local `dotnet-ef`,
  Infrastructure as target and API as startup project (same tooling
  `001-foundation` established), generate the new EF Core migration adding
  the `Users` and `UserProfiles` tables and their constraints from T006
  under `src/GaussAuth.Infrastructure/Persistence/Migrations/`. Inspect the
  generated migration and snapshot, then rename the generated files to
  `<name>.<type>.cs` without changing migration IDs or generated class
  identities (same convention as the existing
  `20261001012324_InitialIdentityFoundation.migration.cs`). Confirm the
  migration adds only `Users`/`UserProfiles` schema — no application,
  membership, role, permission, or session table (FR-016).
- [ ] T011 Create `src/GaussAuth.Api/Users/userResponse.dto.cs` with the
  shared `UserResponse` shape from `contracts/users-api.md`: `Id` (`Guid`),
  `Email` (`string`), `NormalizedEmail` (`string`), `IsActive` (`bool`),
  `CreatedAt`/`UpdatedAt` (`DateTimeOffset`), and a nested `Profile` object
  with `FirstName`, `LastName`, `DisplayName`, `PhoneNumber` (nullable),
  `AvatarReference` (nullable), `CreatedAt`/`UpdatedAt`. This type MUST NOT
  include a password hash, security stamp, internal credential token, or any
  other sensitive Identity infrastructure field (FR-011).
- [ ] T012 Add
  `tests/GaussAuth.Foundation.Tests/usersSchemaMigrationTests.test.cs`:
  apply the migration from T010 against the PostgreSQL 17 development
  service, reapply it and assert no further schema change is produced,
  assert the `Users` and `UserProfiles` tables exist with the shared-key
  foreign keys described in T006, assert the unique index on
  `Users.NormalizedEmail` exists, and assert no application, membership,
  role, permission, or session table was introduced. Run it only against the
  reproducible development service, mirroring
  `tests/GaussAuth.Foundation.Tests/migrationTests.test.cs` from
  `001-foundation`.

**Checkpoint**: Domain, ports, persistence, migration, and the shared
response contract exist. Every user story below can now be implemented.

---

## Phase 3: User Story 1 - Provision a global user identity and profile (Priority: P1) 🎯 MVP

**Goal**: Create a global user with a unique login email, an initial
password, and a required profile in one consistent, rate-limited operation.

**Independent Test**: Submit a creation request with a unique email, a
password, and required profile fields; confirm a user and profile exist
with a stable identifier and that a second request for the same normalized
email (including concurrently) is rejected, and that exceeding the rate
limit is rejected without creating anything.

- [ ] T013 [US1] Add `tests/GaussAuth.Foundation.Tests/createUserTests.test.cs`
  using `Microsoft.AspNetCore.Mvc.Testing` against `POST /users`: (1) valid
  email + password + required profile fields (`firstName`, `lastName`,
  `displayName`) → `201 Created` with a `Location` header and a
  `UserResponse` body containing no password or credential-internal field;
  (2) an email differing only by case/whitespace from an existing user →
  `409 Conflict`, no new user created; (3) a malformed email (e.g.
  `"not-an-email"`) → `400` `ValidationProblemDetails` identifying the email
  field specifically (FR-006); (4) a missing required field or a password
  failing Identity's policy → `400` `ValidationProblemDetails`, no partial
  user/profile/credential row persisted; (5) two concurrent requests for the
  same normalized email → exactly one `201`, the other `409`; (6) a caller
  exceeding the configured rate limit (test-configured to a low permit
  limit via `WithWebHostBuilder`/configuration override, per FR-024) →
  `429 Too Many Requests` with a `Retry-After` header, no user created.
  Confirm these tests compile but fail (or do not yet exist as routes)
  before T014-T021 are implemented.
- [ ] T014 [US1] Create
  `src/GaussAuth.Application/Users/CreateUser/createUser.command.cs` with a
  `CreateUserCommand` carrying the submitted (not yet normalized) email,
  password, and profile fields (`firstName`, `lastName`, `displayName`,
  `phoneNumber`, `avatarReference`).
- [ ] T015 [US1] Create
  `src/GaussAuth.Application/Users/CreateUser/createUser.result.cs` with a
  `CreateUserResult` exposing named outcomes: `Success(User user)`,
  `DuplicateEmail()`, `ValidationFailed(IReadOnlyDictionary<string,
  string[]> errors)` — a small, operation-specific type, not a generic
  result framework (per `research.md`'s "Expected-failure modeling"
  decision).
- [ ] T016 [US1] Create
  `src/GaussAuth.Application/Users/CreateUser/createUser.handler.cs`
  implementing the flow from `research.md`'s "Transaction strategy for user
  creation": normalize the email via
  `ICredentialProvisioningService.NormalizeEmail`; if
  `IUserRepository.ExistsByNormalizedEmailAsync` is true, return
  `DuplicateEmail()`; otherwise mint a new `Guid`, open one
  `AuthenticationDbContext` transaction spanning
  `ICredentialProvisioningService.CreateCredentialAsync`,
  `User.Create(...)` + `IUserRepository.AddAsync`, and
  `IUserRepository.SaveChangesAsync`; roll back and return the appropriate
  `CreateUserResult` on any failure (duplicate detected by the persistence
  unique index counts as `DuplicateEmail()`, not an unhandled exception),
  commit and return `Success(user)` otherwise. No partial user, profile, or
  credential row may survive a failed attempt (FR-008). Log one structured
  `ILogger` entry per invocation containing only the resulting user id and
  outcome (e.g. "created" or "duplicate rejected") — never the email,
  password, or password hash (FR-021, per `research.md`'s "Operation
  logging" decision).
- [ ] T017 [US1] Create `src/GaussAuth.Api/Users/createUserRequest.dto.cs`
  with Data Annotations matching `data-model.md`'s "Request/response field
  limits" table: `Email` required, max length 256, `[EmailAddress]` format
  validation (FR-006); `Password` required, max length 128; `FirstName`
  required, max length 100; `LastName` required, max length 100;
  `DisplayName` required, max length 100; `PhoneNumber` optional, max length
  32; `AvatarReference` optional, max length 2048.
- [ ] T018 [US1] Extend
  `src/GaussAuth.Api/DependencyInjection/apiServiceCollectionExtensions.extension.cs`
  to add `services.AddRateLimiter(...)` with a named fixed-window policy
  `"user-creation"`, partitioned by remote IP address, reading
  `RateLimiting:UserCreation:PermitLimit` (default `5`) and
  `RateLimiting:UserCreation:WindowSeconds` (default `60`) from
  `IConfiguration` rather than hard-coding them (FR-024, per `research.md`'s
  "Rate limiting for anonymous credential creation" decision). Do not apply
  this policy to any route in this task; only register it.
- [ ] T019 [US1] Create `src/GaussAuth.Api/Users/usersEndpoints.extension.cs`
  with a `MapUsersEndpoints(this IEndpointRouteBuilder app)` extension
  mapping `POST /users` with `.RequireRateLimiting("user-creation")` (policy
  from T018): validate the request DTO (via `TypedResults.ValidationProblem`
  on failure), dispatch `CreateUserCommand` to its handler, and translate
  `CreateUserResult` to `201 Created` (with `Location: /users/{id}` and a
  `UserResponse` body built from T011), `409` (duplicate email), or `400`
  (`ValidationProblemDetails`), per `contracts/users-api.md`. A request
  rejected by the rate limiter is handled by the middleware itself
  (`429 Too Many Requests` with `Retry-After`) and never reaches this
  handler logic.
- [ ] T020 [US1] Edit `src/GaussAuth.Api/Program.cs` to call
  `app.UseRateLimiter();` (before endpoint mapping) and
  `app.MapUsersEndpoints();` alongside the existing `/health/live` mapping,
  keeping `Program.cs` a readable composition root with no declared
  top-level type.
- [ ] T021 [US1] Extend
  `src/GaussAuth.Api/DependencyInjection/applicationServiceCollectionExtensions.extension.cs`
  to register the `CreateUser` handler (scoped).
- [ ] T022 [US1] Run
  `tests/GaussAuth.Foundation.Tests/createUserTests.test.cs` and the
  corresponding `quickstart.md` section 4 "create" and rate-limit scenarios;
  confirm all six acceptance scenarios from spec User Story 1 (including
  FR-024's rate limit) pass.

**Checkpoint**: US1 passes independently; a user and profile can be created,
validated (including email format), protected against duplicate/concurrent
normalized email, and rate-limited per caller.

---

## Phase 4: User Story 2 - Retrieve a user and profile (Priority: P1)

**Goal**: Retrieve a previously created user's identity and profile by
stable identifier, excluding credential internals.

**Independent Test**: Retrieve a known user by identifier and confirm the
response exposes identity and profile fields while withholding credential
internals; retrieve an unknown identifier and confirm a safe not-found
result.

- [ ] T023 [US2] Add
  `tests/GaussAuth.Foundation.Tests/userRetrievalTests.test.cs` against
  `GET /users/{id}`: an existing user returns `200 OK` with a `UserResponse`
  containing the identifier, login email, normalized email, active state,
  timestamps, and profile fields, and excludes password hashes, security
  stamps, and other internal credential details; an unknown identifier
  returns `404 Not Found` without revealing persistence internals. Confirm
  this test compiles but fails before T024-T027 are implemented.
- [ ] T024 [US2] Create
  `src/GaussAuth.Application/Users/GetUser/getUser.query.cs` with a
  `GetUserQuery` carrying the requested `Guid` id.
- [ ] T025 [US2] Create
  `src/GaussAuth.Application/Users/GetUser/getUser.handler.cs` calling
  `IUserRepository.GetByIdAsync` and returning the `User?` (null meaning
  "not found", left to the API layer to translate to `404`). Log one
  structured `ILogger` entry per invocation containing only the requested
  user id and whether it was found (FR-021).
- [ ] T026 [US2] Extend
  `src/GaussAuth.Api/Users/usersEndpoints.extension.cs` (from T019) with
  `GET /users/{id}`: dispatch `GetUserQuery`, return `200 OK` with a
  `UserResponse` (T011) when found, `404 Not Found` with generic Problem
  Details when not, per `contracts/users-api.md`.
- [ ] T027 [US2] Extend
  `src/GaussAuth.Api/DependencyInjection/applicationServiceCollectionExtensions.extension.cs`
  to register the `GetUser` handler (scoped).
- [ ] T028 [US2] Run
  `tests/GaussAuth.Foundation.Tests/userRetrievalTests.test.cs` and the
  corresponding `quickstart.md` section 4 "retrieve" scenario; confirm both
  acceptance scenarios from spec User Story 2 pass.

**Checkpoint**: US1 and US2 both pass independently; created users and
profiles are retrievable and safe to expose.

---

## Phase 5: User Story 3 - Update permitted profile information (Priority: P2)

**Goal**: Update a user's reusable profile fields without affecting login
email or credentials.

**Independent Test**: Update an existing user's profile fields with valid
values and confirm only the profile changes; attempt to change the login
email through the same operation and confirm it is rejected.

- [ ] T029 [US3] Add
  `tests/GaussAuth.Foundation.Tests/profileUpdateTests.test.cs` against
  `PUT /users/{id}/profile`: valid `firstName`/`lastName`/`displayName`
  (each required, max 100 chars per `data-model.md`), optional
  `phoneNumber` (max 32 chars) and `avatarReference` (max 2048 chars) →
  `200 OK` with the profile's `updatedAt` advanced and `email`/`isActive`
  unchanged; a field exceeding its limit or otherwise invalid → `400`
  `ValidationProblemDetails` with the prior profile values unchanged; the
  request DTO has no `email` field, so an attempt to submit one is rejected
  by model binding/validation rather than silently accepted (FR-013); an
  unknown identifier → `404 Not Found`; two sequential updates to the same
  user demonstrate last-write-wins (the second update's values persist,
  consistent with the "Concurrency and consistency" clarification — no
  `409` is produced for a second successful write). Confirm this test
  compiles but fails before T030-T035 are implemented.
- [ ] T030 [US3] Create
  `src/GaussAuth.Application/Users/Profiles/updateProfile.command.cs` with
  an `UpdateProfileCommand` carrying the target `Guid` id and the permitted
  profile fields (`firstName`, `lastName`, `displayName`, `phoneNumber`,
  `avatarReference`) — no email field.
- [ ] T031 [US3] Create
  `src/GaussAuth.Application/Users/Profiles/updateProfile.result.cs` with an
  `UpdateProfileResult` exposing `Success(User user)`, `NotFound()`, and
  `ValidationFailed(...)` outcomes.
- [ ] T032 [US3] Create
  `src/GaussAuth.Application/Users/Profiles/updateProfile.handler.cs`:
  load the user via `IUserRepository.GetByIdAsync`; if null, return
  `NotFound()`; otherwise call `user.UpdateProfile(...)` (T002/T003) and
  `IUserRepository.SaveChangesAsync`, returning `Success(user)`. Never
  touches `Email`/`NormalizedEmail`/credential state (FR-013). Log one
  structured `ILogger` entry per invocation containing only the user id and
  outcome (updated/not found) (FR-021).
- [ ] T033 [US3] Create
  `src/GaussAuth.Api/Users/updateProfileRequest.dto.cs` with Data
  Annotations matching `data-model.md`: `FirstName` required, max length
  100; `LastName` required, max length 100; `DisplayName` required, max
  length 100; `PhoneNumber` optional, max length 32; `AvatarReference`
  optional, max length 2048. No `Email` property exists on this type.
- [ ] T034 [US3] Extend
  `src/GaussAuth.Api/Users/usersEndpoints.extension.cs` with
  `PUT /users/{id}/profile`: validate the request DTO, dispatch
  `UpdateProfileCommand`, translate `UpdateProfileResult` to `200 OK` (with
  the updated `UserResponse`), `404 Not Found`, or `400`
  `ValidationProblemDetails`, per `contracts/users-api.md`.
- [ ] T035 [US3] Extend
  `src/GaussAuth.Api/DependencyInjection/applicationServiceCollectionExtensions.extension.cs`
  to register the `UpdateProfile` handler (scoped).
- [ ] T036 [US3] Run
  `tests/GaussAuth.Foundation.Tests/profileUpdateTests.test.cs` and the
  corresponding `quickstart.md` section 4 "update profile" scenario;
  confirm all three acceptance scenarios from spec User Story 3 pass.

**Checkpoint**: US1, US2, and US3 all pass independently; profile data can
be kept current without ever touching login identity.

---

## Phase 6: User Story 4 - Activate and deactivate a user (Priority: P2)

**Goal**: Deactivate a user so they become ineligible for future
authentication without losing their historical record, and reactivate a
user when access should resume — both operations idempotent.

**Independent Test**: Deactivate an active user and confirm its state
changes while the record and profile remain intact and retrievable;
reactivate it and confirm it returns to active state; repeating either
transition is a safe no-op, not an error.

- [ ] T037 [US4] Add
  `tests/GaussAuth.Foundation.Tests/userActivationTests.test.cs` against
  `POST /users/{id}/activate` and `POST /users/{id}/deactivate`: an active
  user deactivated → `200 OK` with `isActive: false` and `updatedAt`
  advanced, profile/record preserved and still retrievable; deactivating it
  again → `200 OK` with `isActive: false` and `updatedAt` **unchanged**
  (idempotent, not a `409`/error, per spec Clarifications 2026-10-01); an
  inactive user reactivated → `200 OK` with `isActive: true`; reactivating
  it again → `200 OK`, unchanged, idempotent; an unknown identifier on
  either route → `404 Not Found`. Confirm this test compiles but fails
  before T038-T041 are implemented.
- [ ] T038 [US4] Create
  `src/GaussAuth.Application/Users/ActivateUser/activateUser.handler.cs`:
  load the user via `IUserRepository.GetByIdAsync`; if null, signal
  not-found; otherwise call `user.Activate(now)` (T002, idempotent) and
  `IUserRepository.SaveChangesAsync`; return the (possibly unchanged) user.
  Log one structured `ILogger` entry per invocation containing only the
  user id and whether the state actually changed (FR-021).
- [ ] T039 [US4] Create
  `src/GaussAuth.Application/Users/DeactivateUser/deactivateUser.handler.cs`:
  same shape as T038 but calling `user.Deactivate(now)`. Must not perform
  any physical deletion of the user or profile row (FR-014). Log one
  structured `ILogger` entry per invocation containing only the user id and
  whether the state actually changed (FR-021).
- [ ] T040 [US4] Extend
  `src/GaussAuth.Api/Users/usersEndpoints.extension.cs` with
  `POST /users/{id}/activate` and `POST /users/{id}/deactivate`: dispatch to
  the corresponding handler, return `200 OK` with the current
  `UserResponse` on success (whether or not the state actually changed), or
  `404 Not Found` for an unknown identifier, per `contracts/users-api.md`.
- [ ] T041 [US4] Extend
  `src/GaussAuth.Api/DependencyInjection/applicationServiceCollectionExtensions.extension.cs`
  to register the `ActivateUser` and `DeactivateUser` handlers (scoped).
- [ ] T042 [US4] Run
  `tests/GaussAuth.Foundation.Tests/userActivationTests.test.cs` and the
  corresponding `quickstart.md` section 4 "deactivate/activate" scenario;
  confirm all three acceptance scenarios from spec User Story 4 pass.

**Checkpoint**: All four user stories pass independently; the feature is
ready for Polish.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Validate the complete feature and remove accidental scope.

- [ ] T043 Review every file touched by T002-T041 for the constitutional
  reference direction (Domain has no EF Core/ASP.NET Core/Identity
  reference; Application depends only on Domain and its own ports;
  Infrastructure/API implement those ports), the one-type/filename
  convention, explicit field length and format limits matching
  `data-model.md`, the `"user-creation"` rate-limit policy being applied to
  `POST /users` only (not the other four routes, per `research.md`), and
  absence of caller authentication/authorization code (FR-023),
  email-change support, or any application/membership/role/permission/
  session scope; fix only concrete violations. `ArchitectureTests` from
  `001-foundation` already enforces the reference-direction and filename
  rules automatically — use it to confirm, not duplicate it.
- [ ] T044 Run every command in `specs/002-users-profiles/quickstart.md`
  from the existing development container, including the build, both
  migrations (apply and reapply), starting the API, all five `/users`
  scenarios from `contracts/users-api.md`, the rate-limit scenario, the full
  test run, and the stop/recreate lifecycle; update `quickstart.md` to the
  verified commands and expected outcomes.
- [ ] T045 Review API responses and structured logs for the five new
  routes for committed secrets, password/password-hash/security-stamp
  leakage, and unnecessary exposure of persistence or Identity internals
  (FR-020/FR-021); confirm the log line added in T016/T025/T032/T038/T039
  contains only a user id and outcome, never a password, hash, or security
  stamp; confirm no full request payload containing a password is logged;
  correct any finding.
- [ ] T046 Perform the final clean build and full essential test run with
  `GaussAuth.slnx` from the SDK container; confirm exactly two migrations
  exist (`001-foundation`'s initial migration plus this feature's) and that
  the five routes match `contracts/users-api.md` exactly (no extra route);
  stop/remove disposable services with the lifecycle command in
  `quickstart.md`.

---

## Dependencies & Execution Order

### Phase Dependencies

- Phase 1 → Phase 2 → US1 → US2 → US3 → US4 → Polish.
- T002-T003 (Domain) block T004-T009 (ports/Infrastructure depend on the
  entity shapes); T006 blocks T010 (migration needs the mapping); T010
  blocks T012 (schema test needs the migration applied).
- T013 (tests) is written before T014-T021 (implementation) per the
  story's TDD note; the same pattern repeats for T023, T029, and T037.
- T018 (rate-limiter policy registration) blocks T019 (mapping `POST
  /users` with `.RequireRateLimiting(...)`), which in turn needs T020's
  `app.UseRateLimiter()` call to take effect at runtime.
- US2-US4 each reuse `usersEndpoints.extension.cs` (T019) and
  `applicationServiceCollectionExtensions.extension.cs` (T021), extending
  rather than replacing them — later stories must not revert an earlier
  story's route or registration.

### User Story Dependencies

- **US1**: Depends only on the Foundational phase; no other story
  dependency. Independently proves creation, validation (including email
  format), duplicate/concurrent-email rejection, and rate limiting.
- **US2**: Depends on the Foundational phase and reuses US1's
  `usersEndpoints.extension.cs`/DI-registration files, but has an
  independent retrieval contract and test; does not require US1's specific
  test data beyond a user it creates itself for its own test.
- **US3**: Depends on the Foundational phase and the same shared files as
  US2; independently proves profile mutability and email immutability.
- **US4**: Depends on the Foundational phase and the same shared files;
  independently proves idempotent state transitions without deletion.

### Within Each User Story

- Write the story's test file before its implementation tasks; a test that
  cannot compile before a route/handler exists is pending, not evidence of
  a behavioral failure.
- Ports and Domain methods (Foundational) before handlers; handlers before
  endpoint mapping; endpoint mapping before DI registration; DI registration
  before running the story's tests.
- Complete each story's checkpoint before starting the next story.

### Parallel Execution Examples

None. The constitution requires one well-contextualized sequential agent
unless explicit task-specific justification outweighs the token and overlap
cost. No task in this feature has that justification — most story tasks
also edit the same shared `usersEndpoints.extension.cs` and
`applicationServiceCollectionExtensions.extension.cs` files across stories,
which would conflict under parallel execution. No `[P]` marker or parallel-
agent example is supplied.

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Setup and Foundational phases.
2. Complete US1 and validate creation, validation, duplicate/concurrent
   normalized-email rejection, and rate limiting independently.
3. Continue to US2, US3, and US4 only after the US1 checkpoint passes.

### Incremental Delivery

1. US1 makes a global user with a profile creatable, persisted, and
   rate-limited.
2. US2 makes that user and profile readable without leaking credentials.
3. US3 makes the profile safely editable without touching login identity.
4. US4 makes the identity's active/inactive lifecycle controllable without
   deletion.
5. Polish validates the full quickstart and removes accidental scope.

No functional login, session, token, application/membership, role,
permission, password-recovery, email-change, or file-upload capability is
included.

## Notes

- Task IDs are strictly sequential and every story-phase task has its story
  label and a concrete path.
- Testing is limited to the creation, duplicate-email, email-format,
  rate-limit, retrieval, profile-update, activation/deactivation, and
  migration/persistence behaviors identified in the specification;
  `ArchitectureTests` already covers Domain/Application boundary protection
  for the new files without any new test.
- A single agent should finish each checkpoint before moving on;
  independent files alone do not authorize parallel execution.
