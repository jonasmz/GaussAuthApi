# Contract: Migration Command and SQL Script

## Standard path: one-off command from the release image

```text
dotnet GaussAuth.Api.dll migrate
```

(in the container image: `docker run … <image> migrate`, because the image entrypoint is `dotnet GaussAuth.Api.dll`.)

| Aspect | Contract |
|--------|----------|
| Version | Runs from the **same image** (same binary, same migrations assembly) as the API being released. |
| Host | Minimal separate host: registers only the database context. Requires **only** `ConnectionStrings__AuthenticationDatabase`. Does not need signing keys, storage root or any other API setting. |
| Credentials | Supplied at execution time through the environment / mounted secret; never embedded in image or script. |
| Action | Applies pending migrations in order; logs each applied migration id; no-op (exit 0) when none pending. Never drops the database; never touches data outside migration operations. |
| Success | exit code `0`. |
| Failure | exit code `≠ 0`; one structured Critical log naming the failing migration id (when known) and a value-free reason category (`connection`, `authentication`, `migration-failed`, `configuration`); no connection string, password or stack trace in output. |
| Idempotence | Safe to run repeatedly. Concurrent runs rely on EF Core's migration locking for the PostgreSQL provider; this is verified during implementation rather than assumed, and the documentation states that deployments should still run a single migrator at a time. |
| Runtime separation | The web host (`dotnet GaussAuth.Api.dll` without arguments) **never** applies migrations. |

### Deployment order (the documented contract)

1. Run the migration command to completion.
2. **Only if it exited 0**, start the new API version.
3. Wait for `/health/ready` = 204 before routing traffic.

A failed step 1 stops the deployment. The compose reference implements this with `depends_on: { migrate: { condition: service_completed_successfully } }`. If the schema is behind the release the API still starts but reports **not-ready** (never serves from an incompatible schema silently).

## Alternative path: reviewed SQL script

Each release image carries an **idempotent** SQL script generated in the same build as the binary:

```text
/app/db/gaussauth-schema.sql        # inside the image
scripts/generate-migration-script.sh  # regenerates the same file locally (dev container)
```

Extract with `docker create` / `docker cp` (no shell needed in the image).

| Aspect | Contract |
|--------|----------|
| Content | `dotnet ef migrations script --idempotent` over the complete migration chain: exactly the migrations in that release. |
| Equivalence | Applying the script to a database yields the same schema and the same `__EFMigrationsHistory` rows as the migration command from the same starting state. Validated in `quickstart.md`. |
| Nature | An operational alternative for environments where operators/DBAs review and apply changes. It is **not** a separate evolution mechanism and must never be hand-edited. |
| Credentials | None inside; applied with the DBA's own database tooling and identity. |

## Rollback

There is no automated down-migration path in production. Rollback = restore the pre-upgrade database backup (documented in `docs/database.md`), then redeploy the previous image. `Down` methods exist for development only.
