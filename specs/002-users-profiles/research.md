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

**Decision**: `CreateUserHandler` opens one EF Core transaction on
`AuthenticationDbContext` (`Database.BeginTransactionAsync`) that spans: (1)
`ICredentialProvisioningService.CreateCredentialAsync` (wraps
`UserManager.CreateAsync`, which writes through the same `DbContext`
instance because `AddEntityFrameworkStores<AuthenticationDbContext>` shares
it per-request/per-scope), (2) adding the Domain `User` (with its owned
`UserProfile`) via `IUserRepository.AddAsync`, and (3)
`IUserRepository.SaveChangesAsync`. A failure at any step rolls back the
whole transaction, leaving no partial user/credential/profile row.

**Rationale**: Directly satisfies FR-008's "must not leave a partial
user/profile state if persistence fails" using only EF Core's own
transaction API — no additional unit-of-work abstraction or third-party
library is needed because everything already shares one `DbContext`/
connection per request scope.

**Alternatives considered**: A two-phase "create credential, then create
domain row, with manual compensation on failure" would reintroduce the
partial-state risk the requirement explicitly forbids. A distributed/outbox
pattern is unwarranted complexity for a single-database, single-transaction
operation.

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
and field list in `data-model.md`): email 256 chars, password 128 chars
(minimum presence-only here; Identity's configured `PasswordOptions` governs
complexity at credential-creation time), first/last/display name 100 chars
each, phone number 32 chars, avatar reference 2048 chars (URL-length
headroom).

**Rationale**: FR-019 and the constitution's "Endpoints MUST define
reasonable use-case limits... MUST NOT rely solely on framework defaults"
both require explicit, reviewable limits rather than unlimited `string`
fields.

**Alternatives considered**: Relying on PostgreSQL column length alone would
turn a validation concern into a database exception, which FR-019
explicitly disallows as the normal rejection path.
