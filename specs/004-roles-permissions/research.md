# Research: Roles and Permissions

## Application-Scoped Referential Integrity

**Decision**: Store `ApplicationId` on RolePermission and UserRole and enforce composite foreign keys to their parent Role/Permission or Role/ApplicationMembership rows, plus unique pair indexes.

**Rationale**: A simple foreign key to a role and a separate foreign key to a permission proves that each exists but cannot prove that both belong to the same application. Composite keys `(RoleId, ApplicationId)` and `(PermissionId, ApplicationId)` make PostgreSQL reject a cross-application RolePermission. Likewise `(UserId, ApplicationId)` references ApplicationMembership and `(RoleId, ApplicationId)` references Role for UserRole. Application-level state checks still enforce active status, which a foreign key cannot express.

**Alternatives considered**: Application-only checks were rejected because concurrent or direct persistence paths could violate isolation. Database triggers were rejected because composite foreign keys express the immutable context rule without server-side procedural logic.

## Historical Relationship Reactivation

**Decision**: RolePermission and UserRole keep a stable id, active flag, and timestamps. A repeated valid assignment finds and reactivates its inactive historical record; it does not insert a second row.

**Rationale**: This preserves history, meets pair uniqueness, and gives idempotent management behavior. Invalid current state never reactivates a relationship.

**Alternatives considered**: Physical deletion loses history. Creating a new historical row complicates uniqueness and makes effective-permission auditing ambiguous. Permanently rejecting a removed relation makes normal re-assignment impossible.

## Effective Permission Resolution

**Decision**: Resolve effective permissions through one targeted authorization read constrained by user id and application id, filtering every active relationship/state and returning distinct permission codes ordered by code.

**Rationale**: The read models the specification's complete eligibility conjunction and naturally prevents cross-application leakage. Distinctness is applied at the permission identity/code level so permissions granted by several roles appear once.

**Alternatives considered**: Maintaining a materialized permission cache is premature and risks stale eligibility. Resolving permissions separately per role creates unnecessary application-side joins and duplicate elimination work.

## Naming and Validation

**Decision**: Normalize role names with trim plus invariant uppercase comparison value; preserve the trimmed display name. Normalize permission codes with trim plus invariant lowercase and validate `^[a-z0-9]+(?:-[a-z0-9]+)*(?:\.[a-z0-9]+(?:-[a-z0-9]+)*)*$`.

**Rationale**: The role normalization provides case-insensitive uniqueness while retaining a readable display spelling. The permission expression implements the clarified dotted segment format, prevents empty segments and leading/trailing punctuation, and supports examples such as `reports.monthly-read`.

**Alternatives considered**: Case-sensitive names permit equivalent roles. Free-form permission text undermines stable machine identifiers. A fixed two-segment code format rejects legitimate subresource capabilities.

## Endpoint and Error Shape

**Decision**: Follow the existing Minimal API routes and safe Problem Details convention, exposing only context-bound management and query operations.

**Rationale**: Existing application and membership endpoints already map 400/404/409 without persistence details. Every mutation/direct query includes application id in the route; user-role lists and effective permissions include both user and application id.

**Alternatives considered**: Global role/permission endpoints obscure application context. IdentityRole was rejected because it cannot model application-scoped role/permission relationships and would violate domain isolation.

## Development and Verification Workflow

**Decision**: Generate and apply the migration and run all .NET commands inside the existing SDK Docker service, using the existing PostgreSQL 17 container.

**Rationale**: This preserves the constitution's container and reproducibility requirements and follows features 001–003.

**Alternatives considered**: Host tooling and a duplicate database container were rejected by project constraints.
