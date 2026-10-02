# Configuration

Configuration precedence is `appsettings.json`, environment-specific JSON, environment variables, then command line. Production-class means every environment except `Development` and `Testing`. Inject secrets through environment variables, mounted files, or the platform secret facility; no vendor is mandated.

The five Production-class requirements are `ConnectionStrings:AuthenticationDatabase`, either `Sessions:Signing:PrivateKeyPem` or `...PrivateKeyPemFile`, `ProfileImages:RootPath`, `ProfileImages:StorageIsPersistent`, and `DataProtection:KeysPath`. They have no committed default. The signing key is asymmetric ECDSA on NIST P-256 (ES256), distinct from the symmetric per-Application consumer secrets.

| Key | Default / meaning |
|---|---|
| `Sessions:Issuer` | `gaussauth`; non-empty |
| `Sessions:SessionLifetimeMinutes` / `AccessTokenLifetimeMinutes` | 480 / 15; access cannot exceed session |
| `Identity:Lockout:*` | 5 attempts, 5 minutes |
| `RequestLimits:MaxBodyBytes` | 65,536 (1–1,048,576) |
| `ProfileImages:MaxBytes` / `MaxDimension` | 5,242,880 / 4096 |
| `Administration:GlobalAdministratorUserIds:N` | no default; empty means no global authority |
| `Administration:MaxBulkSessionRevocation` | 1000 (1–10,000) |
| `SecurityAudit:RetentionDays` | unset or positive |
| `PasswordRecovery:DeliveryFile` | Development/Testing only |
| `Bootstrap:*` | read only by `bootstrap-admin`; remove password after use |
| `ForwardedHeaders:*` / `HttpsRedirection:HttpsPort` | explicit proxy trust / optional redirect |

Never set `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true`; never run Production as `Development`. Consumer configuration contains `CurrentSecretHash`/`RetiringSecretHash`, never plaintext. Rotate a consumer secret through the admin API, deploy consumers with the replacement during the retirement window, then retire the previous secret.

Rate limits (per client address) are Login, PasswordRecovery, PasswordReset and UserCreation 5/60s; AuthorizationContext and SessionCredentials 600/60s; SigningKeys and SecurityEvents 60/60s; ProfileImageWrite 10/60s; Administration 120/60s. Each group accepts `PermitLimit` and `WindowSeconds` within documented bounds.
