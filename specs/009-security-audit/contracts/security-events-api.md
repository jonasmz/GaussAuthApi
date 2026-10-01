# Security Events API Contract

## `GET /security-events`

Protected read-only SecurityEvent page; no create, update, or delete event routes exist.

### Authorization

A valid bearer session is required. An application caller needs effective `audit.events.read` and can see only its session ApplicationId. A separate configured global reviewer identity with a valid session can see all application and global Auth events. Scope is enforced before data access; role names, membership, and same-named roles do not grant global access.

### Query

`fromUtc`, `toUtc`, `eventType`, `outcome`, `userId`, `applicationId`, `sessionId`, `pageSize`, and opaque `cursor` are optional. Ranges, filter values, page size, and cursor are bounded and validated. Application callers cannot select a different application.

### `200 OK`

```json
{"items":[{"id":"guid","eventType":"authorization.role.assigned","outcome":"succeeded","occurredAtUtc":"timestamp","userId":"guid or null","applicationId":"guid or null","sessionId":"guid or null","consumerApplicationId":"guid or null","correlationId":"safe value or null","subjectType":"safe category or null","subjectId":"guid or null","reason":"safe category or null"}],"nextCursor":"opaque value or null"}
```

Items are newest first and responses are `Cache-Control: no-store`. No free-form metadata or secret is returned.

### Failures

`400` invalid filter/range/cursor/page; `401` invalid session; `403` unauthorized scope; `429` audit-query limit. Failures do not disclose entity or reviewer existence.

## Consumer configuration migration

`AuthorizationConsumers:<code>:CurrentSecretHash` is required and `RetiringSecretHash` is optional. Legacy plaintext keys are rejected at startup. Callers keep the existing plaintext header over the protected channel; Auth verifies active/retiring representations without logging, returning, or persisting the plaintext.
