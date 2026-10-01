# Applications and Memberships API Contract

All routes are for trusted administrative/system callers in this feature. They
add no authentication, roles, or permissions. Error bodies use the existing
safe Problem Details convention and never reveal SQL, stack traces, or EF Core
state. All timestamps are UTC ISO-8601 values.

## Application routes

| Method and route | Request | Success | Expected failures |
|---|---|---|---|
| `POST /applications` | `{ "code": "resto-manager", "name": "Resto Manager" }` | `201` and `ApplicationResponse`; `Location: /applications/{id}` | `400` invalid input; `409` duplicate code. |
| `GET /applications/{id:guid}` | — | `200` application | `404` application absent. |
| `GET /applications/by-code/{code}` | — | `200` application | `404` application absent. |
| `GET /applications?limit={1..100}&cursor={opaque}` | Optional limit/cursor | `200` paged applications | `400` invalid limit/cursor. |
| `POST /applications/{id:guid}/activate` | — | `200` application | `404` application absent. |
| `POST /applications/{id:guid}/deactivate` | — | `200` application | `404` application absent. |

```json
{
  "id": "guid",
  "code": "resto-manager",
  "name": "Resto Manager",
  "isActive": true,
  "createdAt": "2026-10-01T00:00:00+00:00",
  "updatedAt": "2026-10-01T00:00:00+00:00"
}
```

## Membership routes

Mutating, direct retrieval, and activation routes always identify
`applicationId` explicitly. The two list routes are limited operational views;
each returned item includes both relationship identifiers and is not a global
authorization decision.

| Method and route | Request | Success | Expected failures |
|---|---|---|---|
| `POST /applications/{applicationId:guid}/memberships` | `{ "userId": "guid" }` | `201`, membership, `Location: /applications/{applicationId}/memberships/{userId}` | `400` invalid input; `404` user/application absent; `409` duplicate pair or inactive application. |
| `GET /applications/{applicationId:guid}/memberships/{userId:guid}` | — | `200` membership | `404` absent context or membership. |
| `GET /applications/{applicationId:guid}/memberships?limit={1..100}&cursor={opaque}` | Optional limit/cursor | `200` paged application memberships | `400` invalid limit/cursor; `404` application absent. |
| `GET /users/{userId:guid}/memberships?limit={1..100}&cursor={opaque}` | Optional limit/cursor | `200` paged user memberships | `400` invalid limit/cursor; `404` user absent. |
| `POST /applications/{applicationId:guid}/memberships/{userId:guid}/activate` | — | `200` membership | `404` absent context/membership; `409` inactive user/application. |
| `POST /applications/{applicationId:guid}/memberships/{userId:guid}/deactivate` | — | `200` membership | `404` absent context/membership. |

```json
{
  "id": "guid",
  "userId": "guid",
  "applicationId": "guid",
  "isActive": true,
  "createdAt": "2026-10-01T00:00:00+00:00",
  "updatedAt": "2026-10-01T00:00:00+00:00"
}
```

State transitions are idempotent. Parent application/user deactivation does
not rewrite membership `isActive`; effective eligibility requires all three
states to be active. No response offers roles, permissions, tokens, sessions,
or application-specific profile data.

## Paged list response

All collection routes return this bounded envelope. `limit` defaults to 50 and
cannot exceed 100. Results are ordered by ascending stable id. `nextCursor` is
absent when the returned page is final and otherwise resumes after the final
id of the current page.

```json
{
  "items": [],
  "nextCursor": "opaque-string-or-null"
}
```
