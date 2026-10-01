# Quickstart Validation: Authorization Contract

## Prerequisites

Start the existing Docker PostgreSQL and SDK/API stack. Configure two distinct external consumer credentials for two test applications; do not place them in source files, `.env.example`, commands, logs, or responses.

## Scenarios

1. Create one active user, two applications, memberships, roles, permissions, and assignments. Log in to Application A.
2. Call [`POST /auth/authorization-context`](contracts/authorization-context-api.md) as Consumer A. Confirm IDs match the session and only A roles/unique permissions appear.
3. Call with Consumer B's application code or secret and A's user credential. Confirm generic `401` and no context data.
4. Revoke the session or deactivate authorization state; repeat and confirm immediate safe rejection. Change a role/permission and confirm the next context reflects it.
5. Use an equivalent test consumer to allow an operation only when a required returned permission exists, with no Auth database connection or persistence-model reference.
6. Rotate Consumer A's configured secret with a temporary retiring value, switch consumers to the new secret, remove the old value, and confirm it is rejected. Inspect logs for absence of both credential types.

After implementation, run `dotnet test GaussAuth.slnx` in the SDK container.
