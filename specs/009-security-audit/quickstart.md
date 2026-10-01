# Quickstart Validation: Security Audit

Start the existing PostgreSQL and SDK/API Docker stack with private database/signing/recovery values, private test caller secrets, and one-way consumer-secret hashes. Never commit or print real values.

1. Exercise login, session validation/logout, recovery/reset, authorization context, lifecycle, and authorization administration; confirm safe persisted representative events.
2. Force a critical event write failure and confirm state rolls back; force an operational write failure and confirm primary operation completes with safe operational observability.
3. Query as application `audit.events.read`; confirm cursor ordering, size bound, and no foreign/global events. Query as global reviewer; confirm transversal/global visibility only there.
4. Test invalid ranges/cursors/foreign scope/mutation routes for safe rejection.
5. Verify active and retiring consumer hashes accept only their application, then remove retiring hash and reject old secret; reject legacy plaintext config at startup.
6. Verify rate limits, Identity lockout, session/application isolation, headers, request bounds, safe logs/errors, and trace linkage.

7. Confirm `/health/live` returns `X-Content-Type-Options: nosniff`, `Referrer-Policy: no-referrer`, and `X-Correlation-Id`; in production HTTPS deployment confirm HSTS and HTTPS redirection.
8. Confirm `RequestLimits:MaxBodyBytes` and `SecurityAudit:RetentionDays` are injected as deployment configuration. Retention has no automatic purge job.

Run `dotnet test GaussAuth.slnx` inside the SDK container.
