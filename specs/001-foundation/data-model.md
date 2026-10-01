# Data Model: 001-foundation

## Scope

This feature introduces no Domain User, UserProfile, Application,
ApplicationMembership, Role, Permission, Session, or SecurityEvent entity.
It creates only the infrastructure-owned Identity persistence schema needed
to prove EF Core, PostgreSQL, credentials, and lockout integration. Later
features define domain models and application-scoped authorization explicitly.
The initial EF migration and model snapshot are the source of truth for
physical column names and provider types.

## Infrastructure Identity user

**Type**: `IdentityUser<Guid>` used by a roleless
`IdentityUserContext<IdentityUser<Guid>, Guid>` in Infrastructure.

| Field group | Meaning and foundation rule |
|---|---|
| Guid Id | Stable persistence identity; no domain User type is created yet. |
| Email and NormalizedEmail | Future email login identifier; normalized email has a unique database index. This feature creates no user workflow. |
| UserName and NormalizedUserName | Identity store fields; default normalized username uniqueness remains. Future user feature defines how email populates them. |
| PasswordHash, SecurityStamp, ConcurrencyStamp | Identity credential and concurrency infrastructure; never exposed or logged. |
| EmailConfirmed, PhoneNumber, PhoneNumberConfirmed, TwoFactorEnabled | Framework persistence fields only; this feature does not select confirmation, MFA, or phone policies. |
| LockoutEnd, LockoutEnabled, AccessFailedCount | Identity lockout storage; thresholds and duration remain for a later feature. |

**Validation**: The migration must preserve Identity's required keys,
foreign keys, and indexes, plus the unique normalized-email index.
User creation, email normalization workflow, password rules, and disabled
user behavior are not implemented here. Adding a unique index now enforces
the constitutional email uniqueness invariant at the persistence boundary;
the later Users feature must define the normalization and creation path.

## Identity auxiliary records

| Record/table | Relationship | Scope |
|---|---|---|
| User claims | Many to one Identity user | Framework user-side storage, no permission resolution. |
| User logins | Many to one Identity user | Framework schema only; no social login workflow. |
| User tokens | Many to one Identity user | Framework schema only; no access-token format or issuance chosen. |
| EF migration history | Tracks applied migrations | Infrastructure bookkeeping. |

The generated migration MUST NOT introduce built-in Identity role tables,
application membership, permission, session, domain profile, business,
or passkey tables. Set the Identity store schema to Version2 at runtime
and design time; inspect the generated migration and snapshot before
accepting them. These auxiliary records do not authorize any behavior.

## State and lifecycle

There are no end-user state transitions in this feature. The only lifecycle
is infrastructure setup: empty database → initial migration applied →
same migration reapplied with no further schema change. Clean development
recreation may remove the local database volume deliberately.

## Boundary map

- Domain: no persistence or Identity type.
- Application: no persistence or Identity implementation dependency.
- Infrastructure: Identity user context, EF mappings, migration, PostgreSQL provider.
- API: composition and an operational route only; no direct domain or business tables.
