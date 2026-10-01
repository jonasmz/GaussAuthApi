# Research: Authenticated Sessions and Access Credentials

All technical unknowns left open by the clarified specification are resolved here. No open clarification items remain.

## R1. Access credential format and signing algorithm

**Decision**: The access credential is a compact **signed JSON Web Token (JWS, RFC 7515/7519)** signed with **ES256 (ECDSA P-256 + SHA-256)** using an asymmetric key pair. The private key stays inside this service; consuming APIs verify with the public key.

**Rationale**: The clarified decision requires a signed, short-lived token that consuming APIs can verify locally. A symmetric algorithm (HS256) would force every consuming API to hold the signing secret and therefore to be able to forge tokens for any application, which breaks application isolation. An asymmetric algorithm lets consumers verify without being able to mint. ES256 gives small keys and signatures, is implemented by the platform (`System.Security.Cryptography.ECDsa`), and avoids RSA key-size and padding pitfalls. The token is a plain JWT used only as a signed envelope; it is **not** an OAuth 2.0 access token, carries no OAuth/OIDC claims profile (no `scope`, `azp`, `nonce`, ID-token semantics), and no OAuth/OIDC endpoint is introduced.

**Alternatives considered**: HS256 (shared secret, rejected as above); RS256 (larger keys/signatures, no benefit here); PASETO or a hand-rolled envelope (rejected: non-standard ecosystem and the specification forbids custom token encodings); opaque token (rejected by the clarification).

## R2. Library versus hand-written token handling

**Decision**: Add exactly one package to `GaussAuth.Infrastructure` only: **`Microsoft.IdentityModel.JsonWebTokens` 8.23.0** (latest stable on NuGet when this plan was written). Use `JsonWebTokenHandler` to create and validate tokens. No other package is added; the Application and Domain projects gain no package reference.

**Rationale** (Constitution VII necessity review):
- *Necessity*: .NET's shared framework has no JWS/JWT compact serialization or validation. Hand-writing base64url encoding, header parsing, algorithm allow-listing, and signature verification is exactly the "custom token encoding" the specification prohibits and is a classic source of algorithm-confusion and `alg=none` vulnerabilities.
- *Maintenance/security/ecosystem*: maintained by Microsoft in the same family as ASP.NET Core Identity and JwtBearer; widely scrutinized; targets modern .NET.
- *Native alternative*: only `ECDsa`, which is used directly for key handling, but it does not provide the envelope.
- *Architectural impact*: confined behind two Application ports; the package never appears in Domain/Application (enforced by the architecture test, see R12).
- The lighter `JsonWebTokenHandler` is preferred over the legacy `System.IdentityModel.Tokens.Jwt` and over the `Microsoft.AspNetCore.Authentication.JwtBearer` middleware: this service validates tokens itself through a port and does not need a pipeline authentication scheme or the additional package surface.

**Alternatives considered**: `Microsoft.AspNetCore.Authentication.JwtBearer` (extra package and a second validation path that would bypass the Application-layer session checks); hand-rolled JWS on `ECDsa` (rejected, see above).

## R3. Signing key management and publication

**Decision**:
- One active ES256 key pair. The private key is supplied by external configuration as PEM text (`Sessions:Signing:PrivateKeyPem`) or as a path to a PEM file (`Sessions:Signing:PrivateKeyPemFile`, for mounted secrets). Never committed; `.env.example` and `compose.dev.yml` carry only an empty placeholder.
- The key id (`kid`) is the RFC 7638 JWK thumbprint of the public key and is placed in the token header.
- If the environment is **Development** and no key is configured, an **ephemeral in-memory key** is generated at startup with a warning log (tokens stop verifying after a restart; persisted sessions are unaffected). In any other environment a missing or invalid key makes startup fail with a generic message that never includes key material.
- Public key publication: `GET /auth/signing-keys` returns the public key as a JSON Web Key Set so consuming APIs can verify locally. This is a plain RFC 7517 key document, not an OIDC discovery or provider endpoint.
- Key rotation is **not** implemented (YAGNI). Placing `kid` in every token and publishing keys as a set means a later rotation feature can add a second verification key without changing the token or endpoint contract.

**Rationale**: Satisfies "keys externally configurable, never committed" and gives consuming APIs the only thing they need for local verification. The development fallback keeps the existing `docker compose` workflow working without a secret on disk; production cannot silently run with an unintended ephemeral key.

**Alternatives considered**: Out-of-band public key distribution only (rejected: fragile and undocumented for consumers); automatic rotation with overlapping keys (rejected: speculative); persisting a generated key in the database (rejected: private key at rest in the same store the clarification keeps separate from verification).

## R4. Authoritative session-state check and caching strategy

**Decision**: Two layers, with the persisted session as the authority:

1. **Local pre-filter (consuming API)**: verify signature, `iss`, `alg` allow-list, `exp`, and `aud` equal to its own application identifier. This rejects forged, expired, and wrong-application tokens without a network call.
2. **Authoritative check (this service)**: `POST /auth/session/validate` (Authorization: Bearer, body names the caller's application code). The service re-verifies the token, then loads the session and evaluates: not expired, not revoked, `session.UserId`/`session.ApplicationId` equal the token claims, caller's application equals the session's application, user active, application active, and membership active. It returns the identity context or a uniform rejection.
3. **Caching (consuming side only)**: a consuming API MAY cache a *positive* authoritative result keyed by `sid` for a short TTL. Default recommendation **30 seconds, never more than 60 seconds**; never cache negative results beyond the request; never cache beyond the token `exp`. The maximum staleness for revocation and eligibility loss is therefore the consumer's cache TTL (this is the "short caching window" referenced by clarification Q2). This service itself keeps **no cache**: no distributed cache is introduced (Constitution VII), and every call hits the database.
4. A consumer that only verifies locally and never calls the authoritative check is bounded only by the 15-minute token lifetime; the contract states this is **not** acceptable for revocation-sensitive operations.

**Rationale**: Honors the clarified model (local signature verification, persisted session authoritative, deactivation effective at the next authoritative check) with the least machinery. Cost per authoritative check is four primary-key/unique-index lookups, which is acceptable without caching on this service.

**Alternatives considered**: An in-process revocation list pushed to consumers (rejected: needs a broker/propagation channel); per-request validation with no caching (permitted but wasteful; left to the consumer); caching inside this service (rejected: multi-instance staleness and added state).

## R5. Claim set and authorization data

**Decision**: Claims are exactly: `iss`, `sub` (UserId), `aud` (ApplicationId), `sid` (SessionId), `iat`, `exp`. Nothing else: no roles, no permissions, no email, no profile data, no security stamp, no business data.

**Rationale**: Clarification Q3 fixed roles/permissions as dynamically resolved from current application-scoped data (the existing effective-permission lookup from `004-roles-permissions` already excludes inactive roles, permissions, memberships, users, and applications). Putting `aud` as the application identifier gives consumers a standard, cheap isolation check. The lookup contract for consuming APIs is `008-authorization-contract`; this feature intentionally adds no authorization endpoint.

**Alternatives considered**: Embedding permissions with a version stamp (rejected by clarification); including email/display name (rejected: unnecessary PII).

## R6. Lifetimes, relationship, and configuration

**Decision**: Defaults of 15 minutes (access credential) and 8 hours (session, absolute), configured as `Sessions:AccessTokenLifetimeMinutes` and `Sessions:SessionLifetimeMinutes`. A session expires at `CreatedAt + SessionLifetime` and never slides. Every issued token has `exp = min(now + AccessTokenLifetime, session.ExpiresAt)`. Both values are loaded into one immutable `SessionPolicy`, validated at startup: each must be greater than zero and the access credential lifetime must not exceed the session lifetime; otherwise startup fails with a generic message (no silent non-expiring sessions).

**Rationale**: Directly encodes FR-005 and FR-009 and the Q5 clarification; configuration style matches the existing `Identity:Lockout:*Minutes` keys.

## R7. Renewal

**Decision**: `POST /auth/session/renew` accepts a **still-valid** access credential (signature valid, not expired) and, after the same authoritative check as validation, issues a new token for the same session with `exp = min(now + AccessTokenLifetime, session.ExpiresAt)`. No refresh token, no rotation, no replay tracking. The old token simply remains valid until its own `exp` (tokens are not individually tracked; revocation is per session).

**Rationale**: Clarification Q4. Because only the session is revocable, there is no per-token state to rotate or replay-protect; a stolen still-valid token can renew only until the session ends or is revoked, which is the same exposure as the session lifetime the clarification already accepts.

**Alternatives considered**: Refresh tokens with rotation (rejected by clarification); extending the session on renewal (rejected: sessions are absolute).

## R8. Trusted session creation and login integration

**Decision**: `LoginService.AuthenticateAsync` stays unchanged as the only authentication logic. `SessionService.CreateAsync` accepts the `LoginOperationResult` it returned, not raw identifiers. To make the trust structural, `LoginOperationResult.Success(...)` becomes `internal` (it is only called from `LoginService`, in the same assembly), so no code outside the Application assembly can fabricate a "successful authentication". `CreateAsync` rejects a non-success result, then re-verifies, without touching the password, that the user, application, and membership are still active (FR-004), creates the session, issues the credential, and saves. The login endpoint composes the two: authenticate, then create the session; any rejection of the second step returns the same uniform 401 as a failed login. The response is the existing `userId`/`applicationId` body plus the session and credential fields (additive, backward compatible with the 005 tests). No second login endpoint is created.

**Rationale**: Keeps "credential authentication" and "session issuance" separate internally while exposing one public flow, avoids re-validating the password, and prevents client-supplied identity claims from ever reaching session creation (the HTTP layer only forwards the result object).

**Ordering detail**: the access credential is signed before the session row is saved, so if the save fails no credential escapes; an unsaved session simply does not exist.

## R9. Persistence

**Decision**: New `Sessions` table via a reviewable EF Core migration (`AddSessions`), mapped in `AuthenticationDbContext`: primary key `Id`; `UserId`, `ApplicationId`, `CreatedAt`, `ExpiresAt` required; `RevokedAt` nullable. A composite foreign key `(UserId, ApplicationId)` to the existing `ApplicationMemberships` alternate key `AK_ApplicationMemberships_UserId_ApplicationId` (same approach as `UserRoles`) guarantees a session can exist only for a real user/application membership pair; `DeleteBehavior.Restrict`. An index on `(UserId, ApplicationId)` is created by that foreign key and serves future per-user revocation. Revocation is a durable nullable timestamp (`RevokedAt`), not a delete, preserving auditability. There is no last-activity column (sessions do not slide) and no client-metadata column (not justified). Expired rows are retained; a purge job is out of scope.

**Rationale**: Reuses existing relational conventions and keeps the Domain free of EF types (the existing mapping-in-DbContext pattern is followed). The membership foreign key encodes the invariant that a session belongs to exactly one user within one application.

**Alternatives considered**: Hashed token storage (not applicable: tokens are not stored); separate foreign keys to `Users` and `Applications` (weaker than the composite membership key); physically deleting revoked sessions (loses auditability).

## R10. Server-controlled time

**Decision**: Inject .NET's `TimeProvider` (BCL, no package) into `SessionService`, registered as `TimeProvider.System` in the API composition root. Token lifetime is **not** enforced by the JWT library (`ValidateLifetime` is disabled); `SessionService` compares `exp` and the session's `ExpiresAt` against `TimeProvider.GetUtcNow()`, so expiry never depends on client time and is deterministic under test.

**Rationale**: Gives tests a controllable clock without a new package and keeps all expiry decisions in one place.

## R11. Error contract, logging, security events, rate limiting

**Decision**:
- External rejections of any bearer credential use one response shape: `401` Problem Details titled "Access is not valid." with `WWW-Authenticate: Bearer`; no body detail distinguishes malformed, forged, expired, revoked, unknown, mismatched, or ineligible. Responses carrying a token (login success, renewal) set `Cache-Control: no-store`. Unexpected failures continue through the existing `SafeExceptionHandler`.
- Internally, a closed `SessionRejectionReason` enum distinguishes causes for logs and events (never exposed).
- Extend `SecurityEventType` with `SessionCreated`, `AccessRenewed`, `SessionRevoked`, `LogoutCompleted`, `AccessRejectedExpired`, `AccessRejectedRevoked`, `AccessRejectedInvalidState`, `AccessRejectedApplicationMismatch`, and add a `Guid? sessionId` parameter to `ISecurityEventRecorder.RecordAsync` (existing callers pass `null`). The existing logging recorder keeps being the implementation (no audit store; `009-security-audit` consolidates). Rejections of tampered/malformed credentials (no trustworthy identifiers) are logged at Debug only, to avoid attacker-driven log flooding; they produce no security event.
- Logs and events carry only `UserId`, `ApplicationId`, `SessionId`, event type, and reason. Credentials are never logged; the Authorization header is never echoed.
- Rate limiting: `login` keeps its existing policy. The new anonymous `GET /auth/signing-keys` gets its own generous per-IP fixed-window policy (default 60 requests per 60 seconds, configurable). `validate`, `renew`, and `logout` are not anonymous in practice: a token with an invalid signature is rejected before any database access, so they are not rate limited (a per-IP limit would break consuming APIs calling from one address), consistent with the requirement not to rate limit authenticated access indiscriminately.

## R12. Architectural isolation from the token technology

**Decision**: Domain `Session` and Application `SessionService` see only `IAccessCredentialIssuer`, `IAccessCredentialValidator`, and `IAccessCredentialKeySet` (returning plain records: `AccessCredentialClaims`, `PublicSigningKey`). The `JsonWebTokenHandler`, `ECDsa`, and key handling live in `GaussAuth.Infrastructure/Sessions`. The existing architecture test's forbidden-reference list is extended with `Microsoft.IdentityModel` and `System.IdentityModel` so any accidental reference from Domain or Application fails the build (SC-011), and its package-reference assertion for Application stays unchanged.

## R13. Logout semantics

**Decision**: `POST /auth/logout` revokes the session identified by a signature-valid credential and returns `204`. A credential that is signature-valid but whose token has expired, whose session is unknown, or whose session is already revoked or expired also returns `204` (idempotent; reveals nothing). A missing, malformed, or forged credential returns the uniform `401`. Accepting an expired-but-genuinely-signed token lets a user end their session even after their token lapsed; this exposes nothing a forger could use and cannot be abused beyond what any holder of a still-valid token can already do.

## R14. Testing approach

**Decision**: Extend the existing MSTest + `WebApplicationFactory` integration suite and add a small domain unit-test class. Tests use a controllable `TimeProvider` registered through `ConfigureServices`, set `RateLimiting__Login__PermitLimit` high (the default of 5 per minute would otherwise throttle multi-login scenarios), and use the Development ephemeral key. "No secrets in logs" is verified with an in-test logger provider that captures output and asserts no credential substring. See `plan.md` for the covered behaviors.

## R15. Development environment

**Decision**: No new containers. The `postgres` and `sdk` services from `compose.dev.yml` are reused; all .NET work (restore, build, test, `dotnet ef migrations add AddSessions`/`database update`) runs inside the `sdk` container. `compose.dev.yml` forwards an optional `SESSIONS_SIGNING_KEY_PEM` variable (empty by default) to `Sessions__Signing__PrivateKeyPem`; `.env.example` documents it as an optional placeholder. No global Docker cleanup is run.
