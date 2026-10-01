# Data Model: Roles and Permissions

## Role

| Field | Rules |
|---|---|
| Id | Stable non-empty identifier. |
| ApplicationId | Required existing application identifier. |
| Name | Required immutable trimmed display name; 1–200 characters. |
| NormalizedName | Required immutable invariant-uppercase comparison value. |
| Description | Optional mutable description, maximum 500 characters. |
| IsActive | Initially true; activation/deactivation is idempotent. |
| CreatedAt / UpdatedAt | UTC timestamps; `UpdatedAt` changes only on a state or description change. |

**Uniqueness**: `(ApplicationId, NormalizedName)` is unique. The same normalized name is permitted in another application.

**Transitions**: Active ↔ Inactive. Inactive roles remain queryable, cannot receive new or reactivated UserRole/RolePermission assignments, and do not grant permissions.

## Permission

| Field | Rules |
|---|---|
| Id | Stable non-empty identifier. |
| ApplicationId | Required existing application identifier. |
| Code | Required immutable canonical lowercase capability code, 3–128 characters. |
| Description | Optional mutable description, maximum 500 characters. |
| IsActive | Initially true; activation/deactivation is idempotent. |
| CreatedAt / UpdatedAt | UTC timestamps; `UpdatedAt` changes only on a state or description change. |

**Code rule**: `^[a-z0-9]+(?:-[a-z0-9]+)*(?:\.[a-z0-9]+(?:-[a-z0-9]+)*)*$` after trim/lowercase normalization.

**Uniqueness**: `(ApplicationId, Code)` is unique. The same code is permitted in another application.

**Transitions**: Active ↔ Inactive. Inactive permissions remain queryable, cannot be newly assigned or reactivated on a RolePermission, and do not grant effective permissions.

## RolePermission

| Field | Rules |
|---|---|
| Id | Stable non-empty identifier. |
| ApplicationId | Required context and must match Role and Permission application ids. |
| RoleId | Required role identifier. |
| PermissionId | Required permission identifier. |
| IsActive | Initially true; removal deactivates; valid repeat assignment reactivates this same row. |
| CreatedAt / UpdatedAt | UTC timestamps. |

**Integrity**:

- Unique `(RoleId, PermissionId)`.
- Composite foreign keys `(RoleId, ApplicationId)` and `(PermissionId, ApplicationId)` guarantee the two parents share the stored application context.
- Assign/reassign requires active application, role, and permission.

## UserRole

| Field | Rules |
|---|---|
| Id | Stable non-empty identifier. |
| ApplicationId | Required context and must match role and membership application ids. |
| UserId | Required global user identifier. |
| RoleId | Required role identifier. |
| IsActive | Initially true; removal deactivates; valid repeat assignment reactivates this same row. |
| CreatedAt / UpdatedAt | UTC timestamps. |

**Integrity**:

- Unique `(UserId, RoleId, ApplicationId)`.
- Composite foreign key `(UserId, ApplicationId)` references ApplicationMembership, and `(RoleId, ApplicationId)` references Role.
- Assign/reassign requires active user, application, membership, and role.

## Effective Permission Set

This is a query result, not a persisted entity. It returns distinct active Permissions for one explicit `UserId` and `ApplicationId` only when all of these are active:

1. User;
2. Application;
3. ApplicationMembership;
4. UserRole;
5. Role;
6. RolePermission;
7. Permission.

Results contain unique permission ids/codes in ascending code order. Inactive or cross-application data contributes nothing.

## Referential-Integrity Summary

```text
Application 1 ── * Role
Application 1 ── * Permission
Role 1 ── * RolePermission * ── 1 Permission
User 1 ── * UserRole * ── 1 Role
User + Application 1 ── 1 ApplicationMembership
```

All normal removal is an `IsActive` transition; no authorization history is physically deleted.
