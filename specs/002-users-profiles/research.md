# Research: 002-users-profiles

All decisions below apply only to this feature. They do not select token
format, session design, application-membership modeling, or other decisions
explicitly left open for later features by the constitution.

## Domain/Identity reconciliation

**Decision**: The Domain `User` aggregate's `Id` (`Guid`) is the same value as
its corresponding `IdentityUser<Guid>.Id` in `AspNetUsers`. `Users.Id` is
configured as a non-generated primary key with a one-to-one foreign key to
`AspNetUsers.Id` (`DeleteBehavior.Cascade`, since a domain user has no
meaning without its Identity credential row). `UserProfile.Id` is in turn the
same value as its owning `User.Id` (one-to-one shared key), which makes
"a user must not have multiple competing profiles" a schema-level guarantee
rather than an application-level check.

**Rationale**: This satisfies FR-018 ("the relationship between the domain
user, its Identity credential representation, and its profile MUST remain
stable and reconcilable") without the Domain depending on Identity types and
without generating two independent identifiers that later need matching
logic. `UserManager<IdentityUser<Guid>>.CreateAsync` already lets the caller
supply the new `IdentityUser<Guid>.Id` up front, so the same `Guid` can be
minted once in Application and handed to both the credential call and the
domain `User.Create(...)` factory.

**Alternatives considered**: A Domain-generated `Guid` reconciled later via a
lookup table would add a second indirection with no benefit, since both
sides are created together inside the same operation. Letting Identity
generate the id and reading it back would still require passing it into the
domain user afterward; supplying it up front is simpler and avoids a
read-after-write.

## EF Core naming collision to avoid

**Finding**: `AuthenticationDbContext` already inherits
`IdentityUserContext<IdentityUser<Guid>, Guid>`, which declares its own
public `DbSet<IdentityUser<Guid>> Users { get; set; }` property (mapped to
table `AspNetUsers`). The new Domain aggregate must **not** reuse the name
`Users` for its own `DbSet` property on `AuthenticationDbContext`, or it will
shadow/collide with the inherited Identity `Users` property.

**Decision**: Name the new properties `DbSet<User> DomainUsers` (mapped to
table `Users` via `ToTable("Users")`) and `DbSet<UserProfile> UserProfiles`
(mapped to table `UserProfiles`). Both live in the same
`authenticationDbContext.context.cs` file as the existing class (one type
per file is unaffected; this is the same type gaining two properties and an
`OnModelCreating` addition).

**Rationale**: Prevents an easy-to-miss runtime/model-building ambiguity
between the Identity-owned `AspNetUsers` table and the new Domain-owned
`Users` table, while keeping the actual SQL table name (`Users`) aligned
with the ubiquitous language used in the spec and plan.

## Transaction strategy for user creation

**Decision**: `CreateUserHandler` opens one transaction via
`IUserRepository.BeginTransactionAsync()` — a small Application-owned port
returning `IUserRepositoryTransaction` (`CommitAsync`/`RollbackAsync`/
`IAsyncDisposable`) — spanning: (1)
`ICredentialProvisioningService.CreateCredentialAsync` (wraps
`UserManager.CreateAsync`, which writes through the same
`AuthenticationDbContext` instance because
`AddEntityFrameworkStores<AuthenticationDbContext>` shares it per-request/
per-scope), (2) adding the Domain `User` (with its owned `UserProfile`) via
`IUserRepository.AddAsync`, and (3) `IUserRepository.TrySaveChangesAsync`.
A failure at any step rolls back the whole transaction, leaving no partial
user/credential/profile row. Infrastructure's `UserRepository` implements
`BeginTransactionAsync` by wrapping EF Core's `Database.BeginTransactionAsync`
behind a thin `EfUserRepositoryTransaction` adapter.

**Rationale**: Directly satisfies FR-008's "must not leave a partial
user/profile state if persistence fails." The first draft of this decision
had `CreateUserHandler` call `AuthenticationDbContext.Database
.BeginTransactionAsync` directly — found during `/speckit-implement` to
violate FR-017 (Application depending on a concrete EF Core type). The
fix is a two-method port (`BeginTransactionAsync` plus the small
`IUserRepositoryTransaction` interface) rather than a generic unit-of-work
abstraction: it is scoped to exactly the one operation that needs it, and
Infrastructure's adapter is a few lines wrapping the EF Core type Application
must never see.

**Alternatives considered**: A two-phase "create credential, then create
domain row, with manual compensation on failure" would reintroduce the
partial-state risk the requirement explicitly forbids. A distributed/outbox
pattern is unwarranted complexity for a single-database, single-transaction
operation. A full generic unit-of-work/repository framework spanning every
future aggregate would be speculative given only one transactional operation
exists in this feature.

## Duplicate-email detection across two uniqueness layers

**Finding (discovered during `/speckit-implement`)**: A concurrent duplicate
creation is not reliably caught by the `Users.NormalizedEmail` unique index
alone. `UserManager.CreateAsync` performs its own pre-insert uniqueness
validation against `AspNetUsers` (via the registered `IUserValidator`) and
returns a failed `IdentityResult` with error codes `DuplicateEmail`/
`DuplicateUserName` *before* either request reaches the `Users` table insert;
only a narrower race window would instead surface as a Postgres unique
violation on `AspNetUsers`' own `EmailIndex`/`UserNameIndex`.

**Decision**: `CredentialProvisioningResult` carries an `IsDuplicateEmail`
flag alongside `Succeeded`/`Errors`. `IdentityCredentialProvisioningService`
sets it both when `UserManager.CreateAsync` returns `DuplicateEmail`/
`DuplicateUserName` error codes and when a `DbUpdateException` wraps a
`PostgresException` unique violation on `EmailIndex`/`UserNameIndex`.
`CreateUserHandler` checks this flag before treating any other credential
failure as `ValidationFailed`, mapping it to `CreateUserResult.DuplicateEmail()`
(409) instead of a generic 400 — consistent with FR-007's "concurrent
creation requests... cannot both succeed" and the API contract's `409` row.

**Alternatives considered**: Relying solely on the `Users.NormalizedEmail`
index (the original plan) under-detects this specific race because Identity's
own validator intercepts it first; relying solely on Identity's error codes
would under-detect the rarer true index-violation race. Checking both closes
the gap without adding a new dependency.

## Email trimming before normalization

**Finding (discovered during `/speckit-implement`)**: ASP.NET Core Identity's
default `ILookupNormalizer` (`UpperInvariantLookupNormalizer`) uppercases but
does **not** trim surrounding whitespace. An email submitted with leading/
trailing spaces therefore normalizes to a *different* value than the same
email without them, silently defeating the "case, surrounding whitespace...
differences MUST NOT allow duplicate login identities" requirement (FR-006).

**Decision**: `CreateUserHandler` trims the submitted email
(`command.Email.Trim()`) once, at the top of the handler, before calling
`NormalizeEmail`, checking `ExistsByNormalizedEmailAsync`, or passing it to
`CreateCredentialAsync`/`User.Create` — so the trimmed value is the single
source of truth used everywhere an email is read or written for this
operation.

**Alternatives considered**: Trimming only in the DTO/API layer would leave
Application exposed to the same bug for any other caller of
`CreateUserHandler` (tests, a future internal caller); trimming is a
normalization concern the Application layer should own, not a presentation
concern.

## Email normalization

**Decision**: `ICredentialProvisioningService` exposes
`string NormalizeEmail(string email)`, implemented in Infrastructure by
delegating to the already-registered `ILookupNormalizer` (the same service
`AddIdentityCore` wires up and `UserManager` uses internally for
`NormalizedEmail`/`NormalizedUserName`). `CreateUserHandler` normalizes the
submitted email once via this port before checking
`IUserRepository.ExistsByNormalizedEmailAsync` and before calling
`CreateCredentialAsync`, so the same normalized value is used for the
Application-level duplicate check, the Domain user's `NormalizedEmail`, and
Identity's own `NormalizedEmail`.

**Rationale**: Reusing Identity's configured normalizer (rather than
reimplementing case-folding/normalization rules in Application or Domain)
guarantees the two representations can never silently diverge, satisfying
the spec's note that normalization "may follow ASP.NET Core Identity
conventions where this does not leak Identity implementation details into
the Domain" — the Domain only ever sees a plain `string`.

**Alternatives considered**: Normalizing independently with
`string.ToUpperInvariant()` in Application would duplicate Identity's
behavior and risk drifting from it if Identity's normalizer is ever
reconfigured.

## Expected-failure modeling (no exceptions as control flow)

**Decision**: Each Application handler returns a small, operation-specific
result type (e.g. `CreateUserResult` with named factory members
`Success(...)`, `DuplicateEmail()`, `ValidationFailed(errors)`) instead of
throwing for expected failures. The API layer maps these outcomes to
Problem Details: validation → 400 (`TypedResults.ValidationProblem`),
duplicate email → 409, not found → 404. Unexpected exceptions continue to
flow through the existing `SafeExceptionHandler` from `001-foundation`
unchanged.

**Rationale**: The constitution requires that "expected domain/application
failures MUST map to appropriate API responses" and that "exceptions MUST
NOT serve as ordinary control flow when an expected-result model is
clearer," while also prohibiting an unnecessary "custom result framework."
A minimal, per-operation result type (not a generic `Result<T, TError>`
library) satisfies both: it is concrete, small, and adds no dependency.

**Alternatives considered**: Throwing dedicated exception types
(`DuplicateEmailException`, etc.) for expected cases would use exceptions as
control flow, which the constitution discourages when a result model is
clearer. Adopting a general-purpose result/either library would be an
unjustified dependency for two or three outcome shapes.

## API surface and routing

**Decision**: Five Minimal API routes grouped under `/users`, registered via
a `MapUsersEndpoints(this IEndpointRouteBuilder app)` extension called from
`Program.cs`, mirroring the existing `AddApplication()`/`AddInfrastructure()`
/`AddApiServices()` composition style:

| Method | Route | Purpose |
|---|---|---|
| POST | `/users` | Create a user + profile (FR-008/FR-009) |
| GET | `/users/{id}` | Retrieve user + profile (FR-011) |
| PUT | `/users/{id}/profile` | Replace permitted profile fields (FR-012/FR-013) |
| POST | `/users/{id}/activate` | Activate (idempotent) (FR-014) |
| POST | `/users/{id}/deactivate` | Deactivate (idempotent) (FR-014) |

**Rationale**: `PUT .../profile` takes a full replacement of the mutable
profile fields, avoiding JSON Patch complexity for a small, fixed field set.
Activate/deactivate as action sub-resources (`POST .../activate`) is a
well-established REST convention for state transitions that aren't a plain
field update, and naturally supports the clarified idempotent semantics
(calling it again just re-confirms the same state).

**Alternatives considered**: A single `PATCH /users/{id}` accepting a
partial document (including state) would blur "profile update" and "state
transition" into one contract, which the spec treats as distinct operations
with different rules (state transitions are idempotent; profile edits are
validated field-by-field and must reject an email change). Email-lookup
retrieval (FR-011, "MAY") is not implemented in this feature; no caller
needs it yet, and it can be added later without restructuring.

## Operation logging

**Decision**: Each handler (`CreateUser`, `GetUser`, `UpdateProfile`,
`ActivateUser`, `DeactivateUser`) logs one structured `ILogger` entry per
invocation using only non-sensitive fields — the user id and the outcome
(e.g. "user {UserId} created", "user {UserId} activated",
"duplicate email rejected for creation attempt") — via the standard
`Microsoft.Extensions.Logging` abstraction already used throughout the
solution. No field ever includes the email, password, password hash, or any
Identity security stamp/token.

**Rationale**: FR-021 requires that logging for these operations, if
present, use the existing structured logging foundation and exclude
sensitive values; adding a minimal, consistent log line per operation keeps
these five routes operationally observable the same way `001-foundation`'s
startup/error paths already are, without logging anything the constitution
or FR-021 prohibits.

**Alternatives considered**: Omitting operation logging entirely would
leave these routes less observable than the rest of the solution for no
stated reason. A dedicated audit-event subsystem is unnecessary: the
constitution's mandatory audit-event list (login, lockout, password
change/recovery/reset, session revocation, role/permission change) does not
include user creation/activation in this feature, so plain structured
logging is proportionate.

**Implementation note (discovered during `/speckit-implement`)**: Using
`ILogger<T>` from Application requires a package reference to
`Microsoft.Extensions.Logging.Abstractions` — a first-party, dependency-free
.NET BCL abstraction, not a third-party or framework package. `001-foundation`'s
`ArchitectureTests` originally asserted zero `PackageReference` entries on
`GaussAuth.Application.csproj` (trivially true while Application had no code
yet); that assertion was loosened to allow exactly this one package by name,
while still failing on anything else (EF Core, ASP.NET Core, Npgsql, or any
other package). This is consistent with the constitution's "native .NET
capabilities SHOULD be preferred" and "Logging MUST be structured and use
standard .NET abstractions" guidance, and does not relax the Domain-isolation
or Infrastructure/API-isolation checks in the same test.

## Test project placement

**Decision**: Add feature-specific test files to the existing
`tests/GaussAuth.Foundation.Tests` project rather than creating a second
test project.

**Rationale**: The project already references all four production
assemblies and the exact Microsoft test/MSTest/Mvc.Testing package set this
feature needs; `ArchitectureTests` already scans the whole `tests/` tree
generically. Creating a second test project would duplicate that
configuration for no behavioral benefit and would violate the constitution's
simplicity principle (no speculative infrastructure).

**Alternatives considered**: A dedicated `GaussAuth.UsersProfiles.Tests`
project per feature would only pay off if/when a single shared test project
becomes unwieldy; that is not yet the case after one prior feature.

## Field length limits (explicit, per constitution Security constraint)

**Decision**: Request DTOs enforce these explicit maximums (full rationale
and field list in `data-model.md`): email 256 chars **and valid email
format** (`[EmailAddress]`), password 128 chars (minimum presence-only here;
Identity's configured `PasswordOptions` governs complexity at
credential-creation time), first/last/display name 100 chars each, phone
number 32 chars, avatar reference 2048 chars (URL-length headroom). Overall
request-body size is not given a feature-specific override: every field
above is already length-bounded, so the resulting JSON body for any of the
five routes is small and well within ASP.NET Core/Kestrel's platform
default (30 MB); this is a deliberate choice, not an oversight, because
introducing a separate, smaller body-size limit would add configuration
surface without a concrete risk it mitigates beyond what the per-field
limits already cover.

**Rationale**: FR-006/FR-019 and the constitution's "Endpoints MUST define
reasonable use-case limits... MUST NOT rely solely on framework defaults"
both require explicit, reviewable limits — including email format, not only
length — rather than unlimited or format-unchecked `string` fields.

**Alternatives considered**: Relying on PostgreSQL column length alone would
turn a validation concern into a database exception, which FR-019
explicitly disallows as the normal rejection path. A dedicated, smaller
`RequestSizeLimit` per route was considered and rejected as unneeded
complexity given the per-field limits already bound payload size.

## Rate limiting for anonymous credential creation

**Decision**: `POST /users` is rate-limited using ASP.NET Core's built-in
`Microsoft.AspNetCore.RateLimiting` middleware (shared-framework, no new
package) with a named fixed-window policy `"user-creation"`, partitioned by
remote IP address. The permit limit and window are read from configuration
(`RateLimiting:UserCreation:PermitLimit`, default `5`;
`RateLimiting:UserCreation:WindowSeconds`, default `60`) rather than
hard-coded, registered in
`apiServiceCollectionExtensions.extension.cs`'s `AddApiServices(...)`, with
`app.UseRateLimiter()` added to `Program.cs` before endpoint mapping and
`.RequireRateLimiting("user-creation")` applied only to the `POST /users`
route. A request rejected by the limiter returns the middleware's built-in
`429 Too Many Requests` with a `Retry-After` header; no user, profile, or
credential is created.

**Rationale**: FR-024 and the constitution's "Abuse-prone public or
anonymous endpoints MUST use rate limiting when implemented, including...
anonymous credential operations" directly apply here: `POST /users` creates
an Identity credential (FR-009) and enforces no caller authentication
(FR-023), making it exactly the kind of anonymous credential operation the
constitution requires to be rate-limited. Scoping the limiter to only this
route (not retrieval/update/activation/deactivation) follows the
constitution's own wording, which ties the requirement to operations that
create or handle credentials/abuse-prone public access — the other four
routes act on an already-created identity and carry no comparable
credential-creation risk in this feature.

**Alternatives considered**: Skipping rate limiting and relying on a
deployment-level/network restriction (the position taken before this
decision) would leave the constitutional MUST unenforced by anything this
feature actually implements, which is not an acceptable substitute for an
explicit control. A third-party rate-limiting library was rejected since
the built-in middleware is sufficient and avoids an unjustified dependency.
