# Roles and Permissions API Contract

All routes are management operations for trusted callers in this feature. They add no authentication, role guard, session, token, or consuming-API enforcement. Error responses use safe Problem Details and do not expose persistence details. Collection routes use `limit` 1–100 (default 50) and an opaque cursor ordered by stable identifier.

## Role Routes

| Method and route | Request | Success | Expected failures |
|---|---|---|---|
| `POST /applications/{applicationId}/roles` | `{ "name": "Operator", "description": "Optional" }` | `201` role | `400` invalid input; `404` application absent; `409` inactive application or duplicate normalized name. |
| `GET /applications/{applicationId}/roles/{roleId}` | — | `200` role | `404` absent/mismatched role or application. |
| `GET /applications/{applicationId}/roles` | Optional cursor/limit | `200` page | `400` invalid pagination; `404` application absent. |
| `POST /applications/{applicationId}/roles/{roleId}/activate` | — | `200` role | `404` absent/mismatched role; `409` inactive application. |
| `POST /applications/{applicationId}/roles/{roleId}/deactivate` | — | `200` role | `404` absent/mismatched role. |

## Permission Routes

| Method and route | Request | Success | Expected failures |
|---|---|---|---|
| `POST /applications/{applicationId}/permissions` | `{ "code": "reservations.read", "description": "Optional" }` | `201` permission | `400` invalid input; `404` application absent; `409` inactive application or duplicate code. |
| `GET /applications/{applicationId}/permissions/{permissionId}` | — | `200` permission | `404` absent/mismatched permission or application. |
| `GET /applications/{applicationId}/permissions` | Optional cursor/limit | `200` page | `400` invalid pagination; `404` application absent. |
| `POST /applications/{applicationId}/permissions/{permissionId}/activate` | — | `200` permission | `404` absent/mismatched permission; `409` inactive application. |
| `POST /applications/{applicationId}/permissions/{permissionId}/deactivate` | — | `200` permission | `404` absent/mismatched permission. |

## Assignment and Query Routes

| Method and route | Request | Success | Expected failures |
|---|---|---|---|
| `POST /applications/{applicationId}/roles/{roleId}/permissions/{permissionId}` | — | `201` or `200` reactivated RolePermission | `404` absent parent; `409` inactive parent, duplicate active pair, or cross-application mismatch. |
| `POST /applications/{applicationId}/roles/{roleId}/permissions/{permissionId}/remove` | — | `200` inactive RolePermission | `404` absent/mismatched relationship. |
| `GET /applications/{applicationId}/roles/{roleId}/permissions` | Optional cursor/limit | `200` page | `400` invalid pagination; `404` absent/mismatched role. |
| `POST /applications/{applicationId}/users/{userId}/roles/{roleId}` | — | `201` or `200` reactivated UserRole | `404` absent parent/membership; `409` inactive user/application/membership/role, duplicate active assignment, or cross-application mismatch. |
| `POST /applications/{applicationId}/users/{userId}/roles/{roleId}/remove` | — | `200` inactive UserRole | `404` absent/mismatched relationship. |
| `GET /applications/{applicationId}/users/{userId}/roles` | Optional cursor/limit | `200` page | `400` invalid pagination; `404` absent user or application. |
| `GET /applications/{applicationId}/users/{userId}/effective-permissions` | — | `200` unique permission array | `404` absent user or application; inactive eligibility returns an empty array. |

## Response Shapes

```json
{
  "id": "guid",
  "applicationId": "guid",
  "name": "Operator",
  "description": "Optional",
  "isActive": true,
  "createdAt": "2026-10-01T00:00:00+00:00",
  "updatedAt": "2026-10-01T00:00:00+00:00"
}
```

Permission responses replace `name` with canonical `code`. Relationship responses include `roleId` plus `permissionId` or `userId`, `applicationId`, state, and timestamps. Paginated responses are `{ "items": [], "nextCursor": "opaque-or-null" }`. Effective-permission responses include only permission id, code, and description; no role or user profile data is exposed.
