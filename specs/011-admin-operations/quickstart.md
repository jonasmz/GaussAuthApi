# Quickstart: Validating Administrative Operations

All commands run through the existing Docker development environment (.NET container plus the PostgreSQL 17 container). Use the project's existing compose/dev workflow; do not run global Docker cleanup.

## Prerequisites

- Database and API containers from `compose.dev.yml` are up.
- A user exists whose UserId you place in `Administration__GlobalAdministratorUserIds__0` (environment, never source). That user needs an active membership in some active Application to sign in.
- `SecurityAudit__GlobalReviewerUserId` is not set (startup fails if it is).

## Validation scenarios

1. **Boundary**: Call any `/admin/...` route without a credential (`401`); with a user lacking permission (`403`); with an Application administrator against another Application (`403`, same response whether or not it exists). Confirm the old `/users` and `/applications` routes return `404`.
2. **Bootstrap**: Register an Application as the global administrator; list its permissions and confirm the nine seeded `auth.*` permissions are active. Attempt to create `auth.custom` (`400`) and to deactivate `auth.roles.manage` (rejected).
3. **Application administrator**: Create a role, assign it `auth.roles.manage` and `auth.memberships.manage`, create a membership, assign the role to the user. As that user, manage roles in this Application (success) and try another Application (`403`).
4. **Authorization view**: `GET .../users/{userId}/authorization` matches the permissions returned by the 008 authorization context for the same user and Application.
5. **Users**: List with `limit=2`, follow `nextCursor`, filter `isActive=false`, search `email=`. Deactivate a user with a live session; the session stops working; repeat deactivation (`200`, no second event). Confirm responses contain no hash, stamp, or reset field.
6. **Sessions**: List an Application's sessions, revoke one (unusable next request), revoke a user's sessions in the Application, and check another Application's sessions are untouched; check `hasMore` with a low `MaxBulkSessionRevocation`.
7. **Consumer secrets**: Generate (plaintext shown once), authenticate a context request with it, rotate (new plaintext once), confirm the previous still works, retire previous (it stops, current works), read metadata (no secret fields). Another Application's secret is unaffected. An Application administrator is rejected.
8. **Audit**: Query `/security-events` as the Application reviewer (`auth.security.audit.read`) and as the global administrator; confirm `actorUserId` and target on each change above, and no secret material.
9. **Migration**: Apply migrations to a database containing an Application with an `audit.events.read` role assignment; confirm the same role now holds `auth.security.audit.read` and the reviewer still has access. Run the seeding again to confirm no duplicates.

## Validation status

Every scenario above is executed by `tests/GaussAuth.Foundation.Tests/administrationTests.test.cs` and `administrationMigrationTests.test.cs`, which run the real API pipeline against the PostgreSQL 17 container of `compose.dev.yml` (scenario 9 by migrating down and up). Running them by hand additionally needs a real user's id in `GLOBAL_ADMINISTRATOR_USER_ID`; create that user through the test fixture or a one-off script, never through a source-controlled value.

## Test commands

```bash
docker compose -f compose.dev.yml exec sdk dotnet test tests/GaussAuth.Foundation.Tests --filter "FullyQualifiedName~Administration"
docker compose -f compose.dev.yml exec sdk dotnet test
```

(The SDK container is the `sdk` service; the full suite must pass after the existing tests are moved to `/admin` routes.)

See [contracts/admin-api.md](contracts/admin-api.md) for routes and permissions and [data-model.md](data-model.md) for schema and configuration.
