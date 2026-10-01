# Authorization Context API Contract

## `POST /auth/authorization-context`

### Required headers

| Header | Meaning |
|---|---|
| `Authorization` | `Bearer` user access credential; never log it. |
| `X-GaussAuth-Application-Code` | Consumer's configured application code. |
| `X-GaussAuth-Consumer-Secret` | Independent externally configured service credential for that application; never log or return it. |

### Success: `200 OK`

```json
{
  "userId": "guid",
  "applicationId": "guid",
  "sessionId": "guid",
  "issuedAt": "timestamp",
  "credentialExpiresAt": "timestamp",
  "sessionExpiresAt": "timestamp",
  "roles": [{ "id": "guid", "name": "operator" }],
  "permissions": ["orders.create", "orders.read"]
}
```

Roles are current active assignments and permissions are current unique active codes for exactly the requested application. No PII, Auth persistence model, Identity type, security stamp, key material, or secret is included.

### Failure: `401 Unauthorized`

Missing/invalid consumer credential, wrong consumer application, invalid user credential, expired/revoked session, inactive state, or application mismatch return the established generic unauthorized result with `WWW-Authenticate: Bearer`. The response does not identify the failing check.

### Consumer responsibilities

1. Configure the expected application code and independent service credential outside source control.
2. Forward the user's bearer credential only to this endpoint together with consumer headers.
3. Trust context only after success and verify returned ApplicationId matches consumer configuration.
4. Enforce consumer-owned business permissions by checking `permissions` for the required code.
5. Deny on any failure; never query Auth PostgreSQL, duplicate password checks, or cache context outside the established policy.

### Rotation

Auth accepts a current and temporary retiring secret only for the same application. Deploy the new value to the consumer and remove the retiring value after transition; the request shape and user credentials remain unchanged.
