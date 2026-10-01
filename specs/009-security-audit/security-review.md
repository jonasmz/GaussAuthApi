# Security Review: Security Audit

## Implemented posture to verify

- Endpoint-specific configurable limits protect creation, login, recovery, reset, sessions, signing keys, authorization context, and audit queries; login retains Identity lockout.
- Persisted sessions, dynamic authorization, application isolation, and consumer application binding remain authoritative.
- Consumer secrets use active plus optional retiring one-way representations; plaintext is delivered only out of band during creation/rotation.
- SecurityEvents are append-only, safe, correlated, transaction-classified, and queried by authorized keyset pages.
- Logs/errors use safe IDs/outcomes only; sensitive response data and request dumps are prohibited.
- API-relevant headers, bounded inputs, `no-store` sensitive responses, and production HTTPS/HSTS expectations are documented.

## Implemented controls

- `POST /auth/login`, recovery, reset, session credential endpoints, authorization-context, and `GET /security-events` use separate configurable fixed-window policies. Login also retains ASP.NET Identity account lockout.
- Sessions are checked against expiration, explicit revocation, user/application/membership activity, and application identity on every validation. Password changes/reset revoke active sessions through the established session policy.
- Consumer authentication accepts a distinct, externally provisioned `CurrentSecretHash` and optional `RetiringSecretHash` per application. Verification uses ASP.NET Core Identity `PasswordHasher`; legacy plaintext settings fail startup. Rotation is active-plus-retiring, and removal of the retiring hash invalidates the former secret.
- SecurityEvent records are append-only PostgreSQL rows. Query access requires a valid session plus `audit.events.read` within its application, or the separately configured `SecurityAudit:GlobalReviewerUserId`; application scope is injected before repository access.
- API responses add `X-Content-Type-Options: nosniff`, `Referrer-Policy: no-referrer`, and `X-Correlation-Id`. Production enables HSTS and HTTPS redirection; Development deliberately does not require local TLS termination.
- Kestrel request bodies are bounded by configurable `RequestLimits:MaxBodyBytes` (default 65,536; maximum 1 MiB). Audit page size is 1–100, default 50; event/filter/cursor fields are length and format validated.

## Operations and limitations

Retention is configurable. Any future purge is administrative and separate from normal API flows; this feature adds no scheduler or archive. Runtime database identities need least privilege; migrations may use separately managed schema privileges. No SIEM, broker, external secrets platform, WAF, mTLS, OAuth/OIDC, tracing platform, or automatic archival is introduced. IP/request payloads are not stored by default. Salted hashes cannot prove distinct plaintext provisioning, so operational provisioning policy and cross-application tests enforce independence.
