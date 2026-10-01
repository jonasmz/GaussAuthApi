# Data Model: 002-users-profiles

## Scope

This feature introduces exactly two Domain types — `User` and
`UserProfile` — and the persistence schema needed to store them alongside
the existing Identity user-side foundation from `001-foundation`. It adds no
`Application`, `ApplicationMembership`, `Role`, `Permission`, `Session`, or
`SecurityEvent` schema; those remain for later features.

## Domain: User (aggregate root)

Namespace: `GaussAuth.Domain.Users`. No EF Core, ASP.NET Core, or Identity
reference.

| Field | Type | Rule |
|---|---|---|
| `Id` | `Guid` | Stable identifier; equals the reconciled `IdentityUser<Guid>.Id` (see Persistence Mapping). Set once at creation, never regenerated. |
| `Email` | `string` | The login email as submitted (post-trim), required, max 256 chars. Immutable after creation in this feature (no email-change operation exists yet). |
| `NormalizedEmail` | `string` | Produced by `ICredentialProvisioningService.NormalizeEmail(...)` at creation time; same value Identity stores as its own `NormalizedEmail`. Unique (enforced at persistence; see below). |
| `IsActive` | `bool` | `true` on creation. `Activate()`/`Deactivate()` are idempotent: calling either when already in that state is a no-op that still returns success (clarified 2026-10-01). |
| `CreatedAt` | `DateTimeOffset` | Set once at creation (UTC). |
| `UpdatedAt` | `DateTimeOffset` | Advances on `Activate()`, `Deactivate()`, or a profile change. |
| `Profile` | `UserProfile` | Owned 1:1 child; never null after construction. |

**Behavior** (illustrative; exact method shapes are an implementation
choice, not a contract):

- `User.Create(id, email, normalizedEmail, profile, now)` — factory; throws
  only on domain-invariant violations (e.g. blank email), not on format/
  policy rules already enforced at the Application/API boundary.
- `Activate(now)` — sets `IsActive = true`; no-op (but still succeeds) if
  already active. Updates `UpdatedAt` only when the state actually changes.
- `Deactivate(now)` — sets `IsActive = false`; no-op (but still succeeds) if
  already inactive. Updates `UpdatedAt` only when the state actually
  changes.
- `UpdateProfile(firstName, lastName, displayName, phoneNumber, avatarReference, now)`
  — delegates to `Profile`'s own update method and advances `UpdatedAt`.
  Never touches `Email`/`NormalizedEmail`.

## Domain: UserProfile (owned entity)

Namespace: `GaussAuth.Domain.Users`.

| Field | Type | Rule |
|---|---|---|
| `UserId` | `Guid` | Same value as the owning `User.Id` (shared key). |
| `FirstName` | `string` | Required, max 100 chars. |
| `LastName` | `string` | Required, max 100 chars. |
| `DisplayName` | `string` | Required, max 100 chars. |
| `PhoneNumber` | `string?` | Optional, max 32 chars. No uniqueness or carrier-specific normalization (clarified via spec Assumptions). |
| `AvatarReference` | `string?` | Optional, opaque value (URL or identifier), max 2048 chars. Not validated against an actual file/resource; file storage is a later feature's concern. |
| `CreatedAt` | `DateTimeOffset` | Set once at creation. |
| `UpdatedAt` | `DateTimeOffset` | Advances on every successful update. |

No application-specific business field (employee/branch/customer/
reservation identifiers, department attributes, preferences, permissions) is
ever added here (FR-005).

## State transitions

```text
        Create
          │
          ▼
      ┌────────┐   Deactivate()    ┌──────────┐
      │ Active │ ───────────────► │ Inactive │
      └────────┘ ◄─────────────── └──────────┘
                     Activate()
```

Both transitions are idempotent: invoking the transition that would be a
no-op (e.g. `Deactivate()` while already `Inactive`) succeeds without error
and without changing `UpdatedAt`. No other state exists in this feature;
lockout is explicitly out of scope (FR-015).

## Persistence mapping (Infrastructure)

Extends the existing `AuthenticationDbContext`
(`src/GaussAuth.Infrastructure/Persistence/authenticationDbContext.context.cs`),
which already derives from
`IdentityUserContext<IdentityUser<Guid>, Guid>` and therefore already
exposes an inherited `DbSet<IdentityUser<Guid>> Users` mapped to table
`AspNetUsers`. To avoid colliding with that inherited property name (see
`research.md`), the new properties are:

| New DbSet property | CLR type | Table |
|---|---|---|
| `DomainUsers` | `GaussAuth.Domain.Users.User` | `Users` |
| `UserProfiles` | `GaussAuth.Domain.Users.UserProfile` | `UserProfiles` |

**Relationships**:

- `Users.Id` → one-to-one, non-generated (`ValueGeneratedNever`) primary key
  that is also a foreign key to `AspNetUsers.Id`, `DeleteBehavior.Cascade`.
- `UserProfiles.UserId` → primary key and foreign key to `Users.Id`,
  `DeleteBehavior.Cascade`. This shared-key 1:1 mapping is what makes
  "a user cannot have multiple competing profiles" a schema guarantee.
- A unique index on `Users.NormalizedEmail` is added as the
  Application-visible mirror of Identity's own unique
  `AspNetUsers.NormalizedEmail` index from `001-foundation`; both must stay
  equal for the same row (enforced by always writing them together inside
  one transaction — see `research.md`).

**Validation**: The new migration must add only the `Users` and
`UserProfiles` tables (plus their indexes/constraints) — no application,
membership, role, permission, or session table, consistent with FR-016.

## Request/response field limits (enforced at the API boundary, FR-019)

| Field | Required | Max length |
|---|---|---|
| Email | yes | 256 |
| Password (creation only) | yes | 128 (presence + max length only; complexity is Identity's configured policy) |
| FirstName | yes | 100 |
| LastName | yes | 100 |
| DisplayName | yes | 100 |
| PhoneNumber | no | 32 |
| AvatarReference | no | 2048 |

## Boundary map

- Domain: `User`, `UserProfile` — no persistence or Identity type reference.
- Application: `IUserRepository`, `ICredentialProvisioningService` ports and
  the five use-case handlers — no concrete EF Core or Identity dependency.
- Infrastructure: `AuthenticationDbContext` extension, EF mapping/migration,
  `UserRepository`, `IdentityCredentialProvisioningService` (wraps
  `UserManager<IdentityUser<Guid>>` and `ILookupNormalizer`).
- API: five Minimal API routes, request/response DTOs, mapping of
  Application results to HTTP responses (including Problem Details for
  expected failures).
