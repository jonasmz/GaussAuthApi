# Data Model: Hardening and Release Readiness

This feature adds **no persisted entities and no schema changes** (no new EF Core migration is planned). The "entities" below are operational concepts: configuration, health state, the migration chain, and the validation record. A corrective migration is allowed only if the migration review finds a release-blocking defect.

## Release Configuration

The complete set of externally supplied settings for one environment. Full key list, defaults and Production requirements: [contracts/configuration-reference.md](contracts/configuration-reference.md).

| Concept | Rules |
|---------|-------|
| Environment class | `Development` and `Testing` are *non-production*; every other name is *Production-class* (R1). Unsafe defaults are only reachable in non-production classes. |
| Required in Production-class | database connection; signing key (PEM or PEM file; ECDSA NIST P-256); profile storage root; profile storage persistence declaration; **Data Protection key-ring location**. |
| Optional with validated defaults | session/access lifetimes, lockout, request body limit, per-group rate limits, administration limits, audit retention, global administrator list, forwarded-header trust lists, HTTPS redirect port, image limits. |
| Value rules | lifetimes > 0 and access lifetime ≤ session lifetime; rate limits `PermitLimit ≥ 1`, `WindowSeconds ≥ 1`; request body 1..1 048 576 bytes; image limits > 0; storage and key-ring paths absolute, free of NUL, and the key-ring path creatable/writable at startup (persistence itself is the operator's declared responsibility and is not inferred); global admin ids valid non-empty GUIDs (blank entries ignored); trust lists parse as IPs/CIDRs and never include a match-everything network; legacy `SecurityAudit:GlobalReviewerUserId` must be unset. |
| Failure semantics | Invalid → startup refuses, one Critical log naming each offending **setting key** and a value-free reason, non-zero exit, no stack trace. Secrets never appear. |
| Warnings (start anyway) | no trusted proxies configured; recovery delivery not configured; storage declared non-persistent; (Production-class only). The framework's "key stored without encryption at rest" warning is expected and documented. |

### State: startup

```text
Configure ──validate──► Invalid ──► Critical log + exit ≠ 0
                │
                └─ valid ─► Started (warnings logged) ─► Ready / Not-ready (see Health)
                                         │
                                         └─ SIGTERM ─► Stopping (drain ≤ shutdown timeout) ─► Stopped
```

## Health Status

| Signal | Source | Result |
|--------|--------|--------|
| Liveness | process responsive; no dependencies | Alive (204). |
| Readiness | database reachable **and** no pending migrations **and** profile storage usable | Ready (204) / Not-ready (503). |

Rules:
- Readiness is the conjunction of its checks; any failing check ⇒ not-ready. The failing dependency and cause are logged server-side on state *transitions* only and are never returned.
- Recovery: when the dependency is restored the next uncached evaluation returns ready — no restart needed.
- Responses carry no body, no headers describing dependencies, no versions.
- Cache: each readiness evaluation is reused for a few seconds (≤ 5 s) to bound database load from an unauthenticated endpoint.

```text
Ready ──(db down | schema behind | storage unusable)──► Not-ready
Not-ready ──(all checks pass again)──► Ready
```

## Migration Chain

Ordered list of the 10 existing migrations (ordering by `[Migration]` id):

| # | Id | Feature | Data effect (Up) |
|---|----|---------|------------------|
| 1 | `20261001012324_InitialIdentityFoundation` | 001 | creates Identity tables |
| 2 | `20261001024704_AddUsersAndProfiles` | 002 | creates users/profiles |
| 3 | `20261001043323_AddApplicationsAndMemberships` | 003 | creates tables |
| 4 | `20261001061232_AddRolesAndPermissions` | 004 | creates tables; **one DropIndex** (replaced index) |
| 5 | `20261001085523_AddSessions` | 006 | creates table |
| 6 | `20261001202342_AddSecurityEvents` | 009 | creates table — *previous release schema state* |
| 7 | `20261001232459_AddSecurityEventActor` | 011 | adds actor column/index |
| 8 | `20261002000000_SeedAdministrativePermissions` | 011 | idempotent data seed |
| 9 | `20261002000617_AddApplicationConsumerCredentials` | 011 | creates table |
| 10 | `20261002010000_MoveAuditPermissionToAuthNamespace` | 011 | renames permission, preserves assignments |

Invariants to validate: applies from an empty PostgreSQL 17 database in this order; re-applying is a no-op; final schema from (zero → latest) equals (state 6 + data → latest) in structure; no row of users, memberships, roles, permissions, assignments, sessions, security events or consumer credentials is lost during 6 → 10; the SQL script and the `migrate` command produce the same schema and `__EFMigrationsHistory` rows.

Critical constraints/indexes asserted after migration from zero (names taken from the model snapshot during implementation): unique normalized email; unique application code; unique membership per (user, application); unique role name and permission code per application; unique role-permission and user-role pairs; one consumer credential row per application; session and security-event lookup indexes; the cross-application integrity constraints from 004.

## First Administrator Bootstrap

Operational procedure, not a persisted concept. No new table or column; it creates one ordinary User through the existing create-user use case.

| Rule | Detail |
|------|--------|
| Precondition | user store empty (any existing user ⇒ refuse, exit ≠ 0, nothing written). |
| Inputs | email, password (env or mounted file), optional names — never arguments. |
| Output | exit 0 and the new `UserId` plus the config key to set; no secret printed or logged. |
| Authority | none by itself; only `Administration:GlobalAdministratorUserIds` grants it; empty list ⇒ none. |

```text
empty system ─(migrate)─► schema ─(bootstrap-admin)─► user exists, no authority
      ─(list UserId + restart)─► global administrator ─► normal 011 administration
```

## Release Validation Record

A Markdown document `specs/012-hardening-release/release-validation.md`, created during implementation.

| Section | Content |
|---------|---------|
| Build | exact commands, SDK version, result, warnings reviewed (category → disposition). |
| Tests | full-suite result in Release against clean PostgreSQL 17; mapping from each spec consistency-review bullet to evidence. |
| Migrations | from-zero result, upgrade-with-data result, script equivalence result, per-migration safety table. |
| Runtime | image build, non-root check, SkiaSharp load, compose run, avatar persistence across container replacement, shutdown behavior. |
| Security | secret scan result (tree + history), forwarded-header spoof test, headers/CORS/error-shape checks, rate-limit coverage. |
| Findings | table: id, area, release-blocking? (yes/no with category), disposition (fixed with test / documented limitation). Release-blocking findings may not be "documented". |
| Sign-off | list of open items (must be none that are release-blocking). |

## Operational Documentation Set

Six documents under `docs/` — deployment, configuration, database, bootstrap, security-baseline, limitations — mapped to FR-043 in [research.md](research.md#r19--documentation-set). They link to the contracts rather than duplicating them.
