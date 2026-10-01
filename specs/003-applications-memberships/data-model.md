# Data Model: 003-applications-memberships

## Scope

This feature introduces `Application` and `ApplicationMembership`. It consumes
the existing global `User` and does not duplicate profile or credential data.
No roles, permissions, credentials, sessions, or business-domain data are
introduced.

## Domain: Application

Namespace: `GaussAuth.Domain.Applications`. Plain domain entity with no EF
Core, ASP.NET Core, Identity, or PostgreSQL dependency.

| Field | Type | Rule |
|---|---|---|
| `Id` | `Guid` | Stable identifier, set at creation and never regenerated. |
| `Code` | `string` | Required canonical code; trim and lowercase before storage; 3–64 ASCII letters/digits/single hyphens; begins/ends alphanumeric; immutable. |
| `Name` | `string` | Required trimmed display name, 1–200 non-whitespace characters; immutable in this feature. |
| `IsActive` | `bool` | `true` at creation; state lifecycle is idempotent. |
| `CreatedAt` | `DateTimeOffset` | UTC timestamp set once. |
| `UpdatedAt` | `DateTimeOffset` | UTC timestamp updated only on a real state change. |

`Create`, `Activate`, and `Deactivate` preserve the existing User domain
convention: a repeated state request succeeds without changing the timestamp.

## Domain: ApplicationMembership

Namespace: `GaussAuth.Domain.Memberships`. It contains scalar stable ids, not
framework navigation dependencies. Eligibility is Application-layer policy.

| Field | Type | Rule |
|---|---|---|
| `Id` | `Guid` | Stable identifier, set at creation. |
| `UserId` | `Guid` | Required existing global domain User identifier; immutable. |
| `ApplicationId` | `Guid` | Required existing Application identifier; immutable. |
| `IsActive` | `bool` | Set at creation from parent states; independently idempotent lifecycle. |
| `CreatedAt` | `DateTimeOffset` | UTC timestamp set once. |
| `UpdatedAt` | `DateTimeOffset` | UTC timestamp updated only on a real state change. |

Creation policy: inactive application rejects creation; active application plus
active user creates active membership; active application plus inactive user
creates inactive membership. Activation requires both parents active.
Deactivating a parent does not change membership state. Eligibility is:
`membership.IsActive && user.IsActive && application.IsActive`.

## Relationships and persistence mapping

`AuthenticationDbContext` gains `DbSet<Application>` and
`DbSet<ApplicationMembership>`.

| Table | Key/constraint | Mapping |
|---|---|---|
| `Applications` | PK `Id`; unique `IX_Applications_Code` | `Id` is `ValueGeneratedNever`; `Code` max 64 required; `Name` max 200 required. |
| `ApplicationMemberships` | PK `Id`; unique `IX_ApplicationMemberships_UserId_ApplicationId` | `Id` is `ValueGeneratedNever`; required `UserId` and `ApplicationId`. |
| Membership → User | FK `UserId` → `Users.Id` | Restrict/no-action delete behavior. |
| Membership → Application | FK `ApplicationId` → `Applications.Id` | Restrict/no-action delete behavior. |

The migration adds only these tables, constraints, indexes, and model snapshot
changes. Existing migration tests must be updated from the previous
two-migration/two-domain-table expectation and verify the new unique indexes
and foreign keys.

## Boundary validation

| Input | Required | Limit/format |
|---|---|---|
| Application `code` | Yes | 3–64 chars; trim/lowercase canonicalization; ASCII letters, digits, single hyphens; no leading/trailing hyphen. |
| Application `name` | Yes | Trimmed, 1–200 non-whitespace chars. |
| Route/user/application ids | Yes where route requires | Valid non-empty GUID. |
| Membership body `userId` | Yes | Valid non-empty GUID. |
| Request payload | Yes | Small fixed DTOs only; reject missing/invalid fields before state changes. |

## Application and port boundary

- `IApplicationRepository`: targeted code/id reads, list, add, save, and
  duplicate-safe save.
- `IApplicationMembershipRepository`: targeted pair/id-context reads, lists
  by one user or one application, add, save, and duplicate-safe save.
- Existing `IUserRepository`: used by membership policy to load the global
  user and evaluate its active state.
- Infrastructure maps only expected named unique-constraint violations to
  duplicate results. Foreign keys remain final data-integrity enforcement.
