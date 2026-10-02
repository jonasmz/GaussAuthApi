# Database and persistent state

Use the one-off `migrate` command as the standard migration path. The release SQL script is a reviewed alternative. Migrations never run at API startup, and exactly one migrator must run at a time. Upgrade by backing up, running migrations, deploying, and waiting for readiness. Roll back application/database changes by restoring a verified backup; do not assume an EF migration has a safe automatic down path.

Back up and restore PostgreSQL and the profile-image storage together at a consistent point. If they are mismatched, database avatar references can point to missing files or files can be orphaned; reconcile before serving users. `ProfileImages:RootPath` must be an absolute, writable persistent volume with restrictive ownership. Readiness reports unavailable storage rather than silently storing elsewhere.

Persist `DataProtection:KeysPath` on shared durable storage. Its key files preserve password-reset and other protected state over restarts and replicas. They are secret-grade, owner-readable only, and unencrypted at rest by default.
