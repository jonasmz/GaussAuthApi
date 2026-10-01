# Feature Specification: Application-Scoped Roles and Permissions

**Feature Branch**: `004-roles-permissions`

**Created**: 2026-10-01

**Status**: Draft

**Input**: User description: "Create feature `004-roles-permissions` for the reusable generic Authentication and Authorization API."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Define Authorization Within an Application (Priority: P1)

A trusted administrator registers roles and concrete permissions for one consuming application, so that the application's authorization vocabulary is explicit without affecting any other application.

**Why this priority**: Roles and permissions are the independent foundation for every later authorization decision.

**Independent Test**: Create a role and permission for one application, retrieve them in that application context, and confirm the same names or codes can be registered independently in another application.

**Acceptance Scenarios**:

1. **Given** an active application, **When** an administrator creates a role with a valid name, **Then** the role is stored in that application and is active.
2. **Given** a role name already registered in an application after normalization, **When** an administrator creates an equivalent role name in that application, **Then** the duplicate is rejected.
3. **Given** two different applications, **When** an administrator creates roles with the same normalized name, **Then** both roles are accepted and remain independent.
4. **Given** an active application, **When** an administrator creates a permission with a valid stable code, **Then** the permission is stored in that application and is active.
5. **Given** an inactive application, **When** an administrator attempts to create an active role or permission, **Then** the operation is rejected and no authorization record is created.
6. **Given** an existing role or permission, **When** it is activated or deactivated repeatedly, **Then** the state transition is idempotent and its historical record remains available.

---

### User Story 2 - Compose Roles and Assign Them to Members (Priority: P1)

A trusted administrator assigns active permissions to active roles and assigns active roles to users who have an active membership in the same application.

**Why this priority**: This connects the authorization vocabulary to the people allowed to participate in an application.

**Independent Test**: In one active application, assign a permission to a role and the role to an active member; attempt equivalent duplicate and cross-application assignments and verify only the valid relationship remains.

**Acceptance Scenarios**:

1. **Given** an active role and active permission in the same active application, **When** the permission is assigned to the role, **Then** one active relationship is stored.
2. **Given** a role and permission from different applications, **When** an administrator attempts to associate them, **Then** the operation is rejected and neither application's configuration changes.
3. **Given** an active user with an active membership and an active role in the same active application, **When** the role is assigned to the user, **Then** one active assignment is stored in that application context.
4. **Given** a user without an active membership in an application, **When** an administrator attempts to assign an application role, **Then** the assignment is rejected.
5. **Given** an inactive user, application, membership, or role, **When** an administrator attempts a role assignment, **Then** the assignment is rejected.
6. **Given** an existing role-permission or user-role relationship, **When** an administrator removes it, **Then** the relationship is retained historically but no longer contributes to authorization.

---

### User Story 3 - Resolve Effective Permissions in Explicit Context (Priority: P1)

A consuming application or trusted administrator asks which permissions a user effectively has in one named application, receiving only unique permissions that are currently eligible in that context.

**Why this priority**: The usable authorization outcome is the context-specific effective permission set, not merely stored assignments.

**Independent Test**: Give one active member two roles with an overlapping permission, then resolve permissions for that application and verify the overlap appears once and does not appear in another application.

**Acceptance Scenarios**:

1. **Given** an active user, application, membership, role assignment, role, role-permission relationship, and permission in the same application, **When** effective permissions are requested, **Then** the permission is returned.
2. **Given** a user with multiple roles granting the same permission, **When** effective permissions are requested, **Then** that permission appears exactly once.
3. **Given** an inactive user, application, membership, role assignment, role, role-permission relationship, or permission, **When** effective permissions are requested, **Then** the affected permission is excluded.
4. **Given** a user with roles in two applications, **When** effective permissions are requested for one application, **Then** no role or permission from the other application is returned.
5. **Given** a membership is reactivated, **When** its previously retained active role assignments and all other eligibility conditions are active, **Then** those existing assignments may contribute again without creating a new assignment.

### Edge Cases

- Equivalent role names that differ only in leading/trailing whitespace or letter case are treated as the same name within one application.
- Permission codes are normalized consistently before uniqueness checks; an invalid, blank, or overly long code is rejected.
- Deactivating a role, permission, membership, user, or application preserves relationships but prevents affected permissions from being effective.
- A request for a missing application, user, role, permission, membership, or relationship returns a safe not-found outcome.
- Concurrent attempts to create an equivalent role, permission, relationship, or assignment result in at most one stored active record.
- Removing one role assignment or role-permission relationship never changes a relationship in another application.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST represent a Role with a stable identifier, application context, display name, normalized uniqueness value, optional description, active state, and historical timestamps.
- **FR-002**: The system MUST allow roles to be created, retrieved, listed by application, activated, and deactivated; role names MUST be unique after normalization within an application and MAY repeat in different applications.
- **FR-003**: The system MUST represent a Permission with a stable identifier, application context, stable machine-readable code, optional description, active state, and historical timestamps.
- **FR-004**: The system MUST validate permission codes as required, bounded, machine-readable capability identifiers; permission codes MUST be unique within an application and MAY repeat in different applications.
- **FR-005**: The system MUST preserve the stable role name and permission code on normal display-description changes; it MUST NOT change either implicitly.
- **FR-006**: The system MUST reject creation of an active role or permission for an inactive or nonexistent application.
- **FR-007**: The system MUST represent a RolePermission relationship with role, permission, active state, and historical timestamps; it MUST permit only one relationship for a role-permission pair.
- **FR-008**: The system MUST allow a permission to be assigned only to an active role in the same application when the permission and application are active.
- **FR-009**: The system MUST preserve removed RolePermission relationships historically while excluding inactive relationships, roles, and permissions from effective permissions.
- **FR-010**: The system MUST represent a UserRole relationship with user, role, application context, active state, and historical timestamps; it MUST permit only one user-role relationship in its application context.
- **FR-011**: The system MUST assign a role only when the user, application, membership, and role exist, are active, and belong to the same explicit application context.
- **FR-012**: The system MUST preserve removed UserRole assignments historically while excluding inactive assignments from effective permissions.
- **FR-013**: The system MUST provide roles assigned to a user only for an explicitly identified application.
- **FR-014**: The system MUST calculate effective permissions only for an explicitly identified user and application and only when every required user, application, membership, assignment, role, relationship, and permission state is active.
- **FR-015**: The system MUST return each effective permission at most once, even if multiple active roles grant it.
- **FR-016**: The system MUST prevent roles, permissions, assignments, relationships, and effective permissions from leaking across application contexts.
- **FR-017**: The system MUST reject duplicate and cross-application operations with safe, consistent outcomes that do not expose internal persistence or implementation details.
- **FR-018**: The system MUST retain authorization configuration and assignment history when deactivated or removed; it MUST NOT physically delete those records during normal operations.
- **FR-019**: The system MUST record meaningful authorization-management operations using identifiers and outcomes only, without unnecessary personal data or complete request payloads.
- **FR-020**: The feature MUST NOT add login, logout, sessions, credentials, access tokens, refresh tokens, authentication middleware, consuming-application runtime enforcement, business-domain rules, or administrative frontend behavior.

### Key Entities

- **Role**: A named, active or inactive group of permissions belonging to exactly one application.
- **Permission**: A stable capability code, active or inactive, belonging to exactly one application.
- **RolePermission**: A historically retained association that allows one same-application role to include one permission.
- **UserRole**: A historically retained, application-scoped assignment of one role to one global user.
- **Effective Permission Set**: The duplicate-free capabilities derived for one user in one application after all eligibility states are evaluated.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A trusted administrator can define a valid role and permission for an active application and retrieve each within that same application context in a single management interaction.
- **SC-002**: In repeated and concurrent equivalent creation attempts within one application, exactly one role, permission, role-permission relationship, or user-role assignment is retained.
- **SC-003**: In acceptance scenarios spanning two applications, 100% of returned roles, assignments, and effective permissions belong only to the explicitly requested application.
- **SC-004**: A user holding overlapping permissions through two or more roles receives each eligible permission exactly once.
- **SC-005**: Inactive user, application, membership, role, permission, or authorization relationship states result in zero contribution from the affected authorization path.
- **SC-006**: Every normal deactivation or removal scenario preserves its historical authorization record while preventing that record from granting a current effective permission.

## Assumptions

- The existing global user, application, and application-membership concepts are available and remain authoritative for identity and application participation.
- Role names are normalized by trimming surrounding whitespace and comparing case-insensitively; their display spelling is stable after creation for this feature.
- Permission codes are normalized by trimming and case-normalizing; they use a bounded dot-separated capability format such as `resource.action` and remain stable after creation for this feature.
- Removing a RolePermission or UserRole is a non-destructive deactivation of that relationship, preserving history.
- Reactivating an application membership does not create assignments; retained active assignments can contribute again only if all other effective-permission conditions are active.
- Management operations are used by trusted callers until a later feature provides the final authentication and authorization mechanism.
