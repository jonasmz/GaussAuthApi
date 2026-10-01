# Data Model: Authorization Contract

## Authorization Context

| Field | Rules |
|---|---|
| UserId | Stable global ID; present only after authoritative validation. |
| ApplicationId | Must match authenticated consumer application and user session. |
| SessionId | Linked persisted session; active, unexpired, and non-revoked. |
| IssuedAt / CredentialExpiresAt / SessionExpiresAt | Server-derived timestamps. Credential never outlives session. |
| Roles | Active assigned roles for this application only: stable ID and name. |
| Permissions | Unique sorted effective active permission codes for this application only. |

## Consumer Service Credential

| Attribute | Rules |
|---|---|
| ApplicationCode | One registered application identity. |
| Current secret | External deployment credential; never source-controlled, returned, or logged. |
| Retiring secret | Optional temporary value accepted only for the same application during rotation. |
| Rotation | Add current, transition consumers, remove retiring. Contract and user tokens do not change. |

```text
Consumer credential → Application
User credential → Session → User + Application
User + Application → active Membership → active UserRole → active Role
Role → active RolePermission → active Permission
```

Every context request resolves this graph at request time. Any invalid edge yields no context.
