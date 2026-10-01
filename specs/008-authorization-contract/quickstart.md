# Quickstart Validation: Authorization Contract

## Prerequisites

Start the existing Docker PostgreSQL and SDK/API stack. Configure two distinct external consumer credentials for two test applications through the deployment secret mechanism; do not place them in source files, `.env.example`, commands, logs, or responses. The consumer needs only its application code, its independently configured service credential, a user bearer credential received with a request, and the public authorization-context response.

## Consumer integration flow

1. Provision the consumer's application in Auth and record its stable ID in the consumer configuration.
2. Configure its application code and service credential outside source control; use a different credential for every application.
3. Send `POST /auth/authorization-context` with the user's bearer credential, the configured application code, and the configured service credential. Do not decode or validate the user credential locally.
4. On `200`, verify `applicationId` equals the locally configured stable ID, then retain only the needed stable `userId` as a logical reference in consumer-owned data.
5. Allow an operation only if the public `permissions` list contains that operation's required generic permission; otherwise deny it.
6. On `401`, `429`, malformed data, or transport failure, deny the operation. Do not infer why the request failed and do not use an older context as fallback.
7. Request fresh context whenever current authorization is required. Session revocation, membership changes, assignment removal, and role/permission deactivation apply on the next context request.
8. Do not connect to Auth PostgreSQL, use Auth foreign keys, reference Auth persistence or Identity models, or duplicate bearer-credential validation.
9. Rotate a service credential by adding the new current value, retaining the prior value only temporarily for that same application, deploying consumers, then removing the retiring value.

## Scenarios

1. Create one active user, two applications, memberships, roles, permissions, and assignments. Log in to Application A.
2. Call [`POST /auth/authorization-context`](contracts/authorization-context-api.md) as Consumer A. Confirm IDs match the session and only A roles/unique permissions appear.
3. Call with Consumer B's application code or secret and A's user credential. Confirm generic `401` and no context data.
4. Revoke the session or deactivate authorization state; repeat and confirm immediate safe rejection. Change a role/permission and confirm the next context reflects it.
5. Use an equivalent test consumer to allow an operation only when a required returned permission exists, with no Auth database connection or persistence-model reference.
6. Rotate Consumer A's configured secret with a temporary retiring value, switch consumers to the new secret, remove the old value, and confirm it is rejected. Inspect logs for absence of both credential types.

After implementation, run `dotnet test GaussAuth.slnx` in the SDK container.
