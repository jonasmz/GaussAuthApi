# Non-release-blocking limitations

- Production password recovery needs an operator-supplied delivery adapter; no SMTP or webhook is shipped.
- Only one signing key is active; rotation requires a coordinated redeploy.
- The Data Protection key ring is unencrypted at rest and there are no encrypted-at-rest options.
- There are no built-in HA/load guarantees.
- There is no detailed diagnostics endpoint.

These are documented operational limitations, not release blockers: deployment owners must provide the surrounding secret, delivery, persistence, and availability controls.
