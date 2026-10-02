# Contract: Configuration Reference

Environment variables use `__` for `:` (e.g. `Sessions__Signing__PrivateKeyPemFile`). **Req.** = required in Production-class environments (anything other than `Development`/`Testing`). Secrets are injected externally (environment, mounted file, platform secret); none belongs in the repository. New settings introduced by this feature are marked **NEW**.

## Required in Production-class

Five keys: connection, signing key, profile storage root, profile storage persistence declaration, Data Protection key-ring location.

| Key | Meaning | Validation |
|-----|---------|------------|
| `ConnectionStrings:AuthenticationDatabase` | PostgreSQL 17 connection (**secret**) | parses; host, database, username present |
| `Sessions:Signing:PrivateKeyPem` *or* `Sessions:Signing:PrivateKeyPemFile` | access-credential **signing key: ECDSA on the NIST P-256 curve (JWS ES256)**, private key in PEM (**secret**); the file form is preferred for mounted secrets. Asymmetric and unrelated to the symmetric per-Application consumer secrets below | valid PEM, 256-bit EC; absent ⇒ startup refused (no ephemeral key outside Development) |
| `ProfileImages:RootPath` | absolute directory for sanitized profile images (user content) | absolute, no NUL; usable (readiness) |
| `DataProtection:KeysPath` **NEW** | absolute directory for the ASP.NET Core Data Protection key ring. **Must be on storage that survives application/container recreation.** Required to preserve Identity password-reset credentials and any other Data Protection–protected state across restarts and replicas. Key files are key material: owner-only permissions, treat as secret-grade (unencrypted at rest by default) | absent, relative, NUL-containing or not creatable/writable ⇒ startup refused naming only this key; optional in Development/Testing (ephemeral ring) |
| `ProfileImages:StorageIsPersistent` **NEW** | operator declaration that the root is on persistent storage | absent ⇒ startup refused; `true` ⇒ normal; `false` ⇒ starts with a Warning |

## Optional (validated, with defaults)

| Key | Default | Validation / note |
|-----|---------|-------------------|
| `Sessions:Issuer` | `gaussauth` | non-empty; identical for issue and validate |
| `Sessions:SessionLifetimeMinutes` | 480 | > 0 |
| `Sessions:AccessTokenLifetimeMinutes` | 15 | > 0 and ≤ session lifetime |
| `Identity:Lockout:MaxFailedAccessAttempts` / `DefaultLockoutMinutes` | 5 / 5 | > 0 |
| `RequestLimits:MaxBodyBytes` | 65 536 | 1 … 1 048 576 |
| `ProfileImages:MaxBytes` / `MaxDimension` | 5 242 880 / 4096 | > 0 |
| `Administration:GlobalAdministratorUserIds:N` | none | valid non-empty GUIDs; blank ignored; **empty list ⇒ no global authority (feature 011 decision, unchanged)**; malformed ⇒ startup refused; legacy `SecurityAudit:GlobalReviewerUserId` must be unset. The first id is obtained with the `bootstrap-admin` command ([contract](bootstrap-command.md)) |
| `Bootstrap:AdministratorEmail` / `Bootstrap:AdministratorPassword` *or* `…PasswordFile` / `…FirstName` / `…LastName` **NEW** | none (names default to `Auth` / `Administrator`) | read **only** by the `bootstrap-admin` command, never by the web host; password is secret-grade (mounted file preferred); remove after use |
| `Administration:MaxBulkSessionRevocation` | 1000 | 1 … 10 000 |
| `SecurityAudit:RetentionDays` | unset | > 0 when set |
| `AuthorizationConsumers:<app-code>:CurrentSecretHash` / `RetiringSecretHash` | none | **hash only**, never plaintext (secret-grade); runtime rotation via 011 admin API |
| `PasswordRecovery:DeliveryFile` | unset | **Development/Testing only**; ignored in Production-class |

### Rate limits (`RateLimiting:<Group>:PermitLimit` / `WindowSeconds`) — all validated: `PermitLimit` 1 … 10 000 000, `WindowSeconds` 1 … 86 400

| Group | Protects | Default (per client address) |
|-------|----------|------------------------------|
| `Login` | `POST /auth/login` | 5 / 60 s |
| `PasswordRecovery` | `POST /auth/password/recovery` | 5 / 60 s |
| `PasswordReset` | `POST /auth/password/reset` | 5 / 60 s |
| `UserCreation` | `POST /users` | 5 / 60 s |
| `AuthorizationContext` | consumer authorization / introspection | 600 / 60 s |
| `SessionCredentials` | session validate / renew / logout | 600 / 60 s |
| `SigningKeys` | `GET /auth/signing-keys` | 60 / 60 s |
| `SecurityEvents` | audit query | 60 / 60 s |
| `ProfileImageWrite` | avatar upload/replace/delete | 10 / 60 s |
| `Administration` | whole `/admin` group | 120 / 60 s |

Health endpoints are intentionally outside named policies (see health contract).

## NEW network / persistence settings

| Key | Default | Meaning / validation |
|-----|---------|----------------------|
| `ForwardedHeaders:KnownProxies:N` | empty | trusted proxy IPs; invalid entry ⇒ startup refused |
| `ForwardedHeaders:KnownNetworks:N` | empty | trusted CIDR ranges; `0.0.0.0/0` and `::/0` (trust-anyone) ⇒ startup refused |
| *(both empty)* | — | forwarded headers **ignored**; direct-deployment behavior; Production-class logs a Warning that remote address, HTTPS detection and dependent policies may not represent the real client |
| `HttpsRedirection:HttpsPort` | unset | when set, HTTP→HTTPS redirect is enabled; unset ⇒ no redirect (TLS terminates at the proxy) |
| `ProfileImages:RequirePersistenceDeclaration` | `false` (implicitly `true` in Production-class) | opt-in to the declaration check in Development/Testing |

## Environment variables that must NOT be set

| Variable | Why |
|----------|-----|
| `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` | trusts every proxy; startup validation refuses it unless explicit trust lists are also configured |
| `ASPNETCORE_ENVIRONMENT=Development` in Production | enables ephemeral signing key, file recovery delivery, verbose logging |

## Logging (`appsettings*.json`, overridable by environment)

| Environment | Console format | Levels |
|-------------|----------------|--------|
| Production-class | JSON (structured, scopes include trace id) | default Information; `Microsoft.AspNetCore`, `Microsoft.EntityFrameworkCore` Warning; `Microsoft.Hosting.Lifetime` Information |
| Development | simple | default Debug-capable |

Never logged (verified by test): access tokens, consumer secrets, passwords, reset credentials, full `Authorization` headers, file content, sensitive request bodies, connection strings. EF Core sensitive-data logging stays disabled.

## Precedence

ASP.NET Core default: `appsettings.json` < `appsettings.<Environment>.json` < environment variables < command line. Committed files contain **no secrets** and only safe defaults, so a lower-precedence default can never supply a production secret; secret-bearing and required-in-Production keys have **no** default.
