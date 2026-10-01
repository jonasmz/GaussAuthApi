# Data Model: Security Audit

## SecurityEvent

| Field | Rules |
|---|---|
| Id | Server-generated stable GUID. |
| EventType | Catalog-produced stable identifier, maximum 128 characters. |
| Outcome | `succeeded`, `rejected`, or `failed`. |
| OccurredAtUtc | Server-controlled UTC timestamp. |
| UserId, ApplicationId, SessionId | Nullable stable identifiers, present only when safely known. |
| ConsumerApplicationId | Nullable resolved consumer identity. |
| CorrelationId | Optional built-in trace/request ID, maximum 128 characters. |
| SubjectType, SubjectId, Reason | Optional catalog-approved safe context. |
| Metadata | Optional internally generated allow-listed JSON, maximum 2 KiB; never requests, headers, or credentials. |

Events are append-only; no normal update/delete transition exists. Null ApplicationId denotes a global Auth event.

## SecurityEventCatalog

| Classification | Examples | Reliability |
|---|---|---|
| Critical | User/application/membership lifecycle, roles/permissions/assignments, administrative revocation, consumer secret lifecycle | Event and state commit together where practical; write failure makes operation incomplete. |
| Operational | Login, lockout, session/rejection, recovery, context validation | Write failure is safely logged and observable but does not block primary operation. |

## AuditQuery and scope

`fromUtc`/`toUtc`, stable type/outcome, user/application/session IDs, page size (default 50; max 100), and opaque `(OccurredAtUtc, Id)` cursor are validated before querying. Results order newest first.

| Scope | Requirement | Visibility |
|---|---|---|
| Application | Active session plus `audit.events.read` effective permission | Only matching ApplicationId. |
| Global | Valid session plus separate configured global reviewer identity | All applications and global events. |

## ConsumerSecretRepresentation

Every application has one `CurrentSecretHash` and optional `RetiringSecretHash`: versioned, salted, one-way representations verified by the approved platform hasher. Plaintext is unrecoverable after creation/rotation delivery.

SecurityEvent persistence indexes cover newest-first global, application, user, session, and event-type paths. Existing identity/authorization uniqueness constraints remain unchanged.
