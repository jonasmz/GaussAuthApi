# Implementation Plan: Authenticated Sessions and Access Credentials

**Branch**: `006-sessions-access` | **Date**: 2026-10-01 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/006-sessions-access/spec.md`

## Summary

Turn a successful `005-authentication-login` result into a persisted, revocable, expiring **Session** and issue a signed, short-lived **access credential** (ES256-signed JWT, 15 minutes by default) that references the session through a stable `sid` claim. Consuming APIs verify the signature locally using a published public key and call one authoritative endpoint for revocation/eligibility; the persisted session remains the source of truth, so logout, expiry, and user/application/membership deactivation are honored at the next authoritative check (bounded by a documented consumer cache window of at most 60 seconds). Roles and permissions are not embedded and are resolved dynamically elsewhere. Renewal reuses the still-valid credential (no refresh token). The Domain gets a `Session` entity independent of token technology; the Application layer gets a `Sessions` slice with four focused ports; Infrastructure adds the EF Core repository, one migration, and the signed-token adapter (one new package, `Microsoft.IdentityModel.JsonWebTokens`); the API extends login and adds renew, validate, logout, and a public-keys route.

## Technical Context

**Language/Version**: C# on .NET 10 with nullable reference types enabled.

**Primary Dependencies**: Existing ASP.NET Core Minimal APIs, ASP.NET Core Identity, EF Core 10, Npgsql EF Core provider 10, Data Annotations, `Microsoft.AspNetCore.RateLimiting`, `TimeProvider` (BCL). **One new package**, Infrastructure only: `Microsoft.IdentityModel.JsonWebTokens` 8.23.0, justified in [research.md](research.md) R2 (no shared-framework JWS implementation; hand-rolling is prohibited by the spec and unsafe). Signing key handling uses platform `System.Security.Cryptography.ECDsa`.

**Storage**: Existing PostgreSQL 17 `AuthenticationDbContext`; one new `Sessions` table through a new EF Core migration `AddSessions`. No credential or token is persisted.

**Testing**: Existing MSTest and `Microsoft.AspNetCore.Mvc.Testing` suite, extended with a session domain unit-test class and a session integration-test class using a controllable `TimeProvider`; architecture test extended to forbid token/identity-model assemblies in Domain and Application.

**Target Platform**: Linux .NET 10 SDK container and the existing PostgreSQL 17 container from `compose.dev.yml` (no new containers).

**Project Type**: ASP.NET Core Web API with Domain, Application, Infrastructure, and API projects.

**Performance Goals**: No throughput target. An authoritative check performs at most four key/unique-index lookups (session, user, application, membership) and one signature verification; credential issuance is one signature. Consumers reduce load by caching positive results for up to 60 seconds.

**Constraints**: Credential lifetime 15 minutes and session lifetime 8 hours (absolute, no sliding), both configurable and validated at startup (positive, credential <= session); credential `exp` never exceeds session expiry; only ES256 accepted; roles/permissions never in the credential; every credential rejection returns one uniform `401`; logout is durable and idempotent; credentials, keys, and the `Authorization` header are never logged; signing key externally supplied and never committed (ephemeral key only in Development); no OAuth 2.0/OIDC, refresh tokens, key rotation, distributed cache, or broker.

**Scale/Scope**: One new Domain entity and enum, one new Application slice (`Sessions`: service, result, rejection reason, options, four ports, two small records), one repository and one migration, three Infrastructure token/key classes, one extension of login, one new API slice (four routes plus a bearer-header helper and DTOs), two rate-limit policies, an additive change (new `sessionId` parameter, new event types) to the security-event port and enum, plus proportionate tests. No password recovery, MFA, social login, administrative UI, or audit store.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Constitutional gate | Pre-research | Post-design evidence |
|---|---|---|
| Bounded authentication service | PASS | Sessions and credential issuance/revocation are explicitly within the service's charter; no business data in the credential or session. |
| Hexagonal architecture and vertical slices | PASS | New `Sessions` slice in Application; Domain `Session` knows nothing of tokens; Infrastructure implements `ISessionRepository`, `IAccessCredentialIssuer`, `IAccessCredentialValidator`, `IAccessCredentialKeySet`; API only translates HTTP. Architecture test forbids IdentityModel references in Domain/Application. |
| Domain identity and application isolation | PASS | Session belongs to exactly one user/application (composite membership FK); `aud` carries the application; validation compares credential, session, and caller application; roles/permissions untouched and still application-scoped. |
| Roles, permissions, and sessions | PASS | Session records user, application, creation, expiration, revocation; expired/revoked sessions never authorize; token format and revocation strategy resolved explicitly here (R1, R4, R13). |
| Fixed technology and persistence | PASS | .NET 10, EF Core migration, PostgreSQL 17, Identity unchanged. One dependency added with a documented necessity review. No FKs from business apps; internal FK reuses the membership key. |
| Container development rule | PASS | Reuses `postgres` and `sdk` compose services; all .NET commands (build, test, migrations) run in the `sdk` container; no global Docker cleanup. |
| Security by design | PASS | ES256 only (algorithm allow-list), asymmetric so consumers cannot mint; uniform `401`; secrets from external config; generic startup failure with no key material; no credential in logs; `Cache-Control: no-store` on token responses; rate limits on the public keys route and on the credential-bearing session routes; invalid signatures rejected before any database access. |
| Simplicity and dependency governance | PASS (with tracked item) | No refresh token, rotation, cache, broker, mediator, or OAuth/OIDC. One justified package (see Complexity Tracking). |
| Proportionate testing and error contracts | PASS | Tests cover session expiry/revocation, isolation, eligibility, tampering, logout, log hygiene, persistence; expected failures map to `401`/`204`/`400`; unexpected ones go through `SafeExceptionHandler`. |
| Specification-driven workflow | PASS | Derived from the clarified spec (Q1-Q5); all deferred items (R1-R15) are decided in `research.md` with no silent weakening. |

**Gate result**: PASS before research and after Phase 1 design. Tracked items (see Complexity Tracking): the new dependency, and the new public routes with their rate-limit policies.

## Project Structure

### Documentation (this feature)

```text
specs/006-sessions-access/
├── spec.md
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── sessions-api.md
├── checklists/
│   └── requirements.md
└── tasks.md                         # Created later by $speckit-tasks
```

### Source Code (repository root)

```text
src/
├── GaussAuth.Domain/
│   └── Sessions/
│       ├── session.entity.cs
│       └── sessionState.enum.cs
├── GaussAuth.Application/
│   ├── Sessions/
│   │   ├── Ports/
│   │   │   ├── sessionRepository.interface.cs
│   │   │   ├── accessCredentialIssuer.interface.cs
│   │   │   ├── accessCredentialValidator.interface.cs
│   │   │   ├── accessCredentialKeySet.interface.cs
│   │   │   ├── accessCredentialClaims.result.cs
│   │   │   └── publicSigningKey.result.cs
│   │   ├── sessionPolicy.options.cs
│   │   ├── sessionRejectionReason.enum.cs
│   │   ├── sessionOperationResult.result.cs
│   │   └── sessionService.service.cs
│   ├── Login/
│   │   └── loginOperationResult.result.cs        # Success(...) -> internal
│   └── Security/
│       ├── Ports/securityEventRecorder.interface.cs   # + sessionId parameter
│       └── securityEventType.enum.cs                  # + session/access events
├── GaussAuth.Infrastructure/
│   ├── GaussAuth.Infrastructure.csproj               # + Microsoft.IdentityModel.JsonWebTokens
│   ├── Persistence/
│   │   ├── sessionRepository.repository.cs
│   │   ├── authenticationDbContext.context.cs        # + Sessions mapping
│   │   └── Migrations/                               # AddSessions migration, designer, updated snapshot
│   ├── Sessions/
│   │   ├── accessCredentialSigningKey.service.cs     # key load, kid, public key set (IAccessCredentialKeySet)
│   │   ├── signedAccessCredentialIssuer.service.cs
│   │   └── signedAccessCredentialValidator.service.cs
│   ├── Security/loggingSecurityEventRecorder.service.cs   # + sessionId
│   └── DependencyInjection/
│       └── infrastructureServiceCollectionExtensions.extension.cs  # + SessionPolicy, signing key (eager), ports; optional IHostEnvironment parameter
├── GaussAuth.Api/
│   ├── Login/
│   │   ├── loginEndpoints.extension.cs               # authenticate -> create session -> extended response
│   │   └── loginResponse.dto.cs                      # + session/credential fields
│   ├── Sessions/
│   │   ├── sessionsEndpoints.extension.cs            # renew, validate, logout, signing-keys
│   │   ├── bearerCredentialReader.extension.cs
│   │   ├── validateSessionRequest.dto.cs
│   │   ├── accessCredentialResponse.dto.cs
│   │   ├── sessionContextResponse.dto.cs
│   │   └── signingKeySetResponse.dto.cs
│   ├── DependencyInjection/
│   │   ├── applicationServiceCollectionExtensions.extension.cs  # + SessionService, TimeProvider
│   │   └── apiServiceCollectionExtensions.extension.cs          # + "signing-keys" and "session-credentials" policies
│   └── Program.cs                                    # + MapSessionsEndpoints, pass environment to AddInfrastructure
compose.dev.yml                                       # + optional Sessions__Signing__PrivateKeyPem passthrough
.env.example                                          # + optional SESSIONS_SIGNING_KEY_PEM placeholder (empty)
tests/GaussAuth.Foundation.Tests/
├── sessionDomainTests.test.cs
├── sessionsAccessTests.test.cs
├── mutableTimeProvider.provider.cs                   # test clock
├── architectureTests.test.cs                         # + IdentityModel forbidden prefixes
├── migrationTests.test.cs                            # + Sessions table, foreign key, index
├── startupTests.test.cs                              # + lifetime and signing-key configuration failures
└── authenticationLoginTests.test.cs                  # recording recorder updated for new signature
```

**Structure Decision**: Extend the existing Domain → Application → Infrastructure → API layout with a `Sessions` vertical slice (a name the constitution already lists), mirroring the service/result/port pattern of `Login`, `Roles`, and `Memberships` rather than adding a mediator or generic token abstraction. The Domain gains one entity and one enum. Token technology lives only in `Infrastructure/Sessions` behind ports. `SessionService` reuses the existing user, application, and membership repositories for eligibility rather than adding a join port (KISS; a single projection query is a later optimization if measured). Two filename suffixes are new to the codebase vocabulary and are consistent with its conventions: `.options.cs` (immutable configuration record, the standard .NET term) and `.provider.cs` (test clock); everything else reuses existing suffixes.

## Behavior Coverage Map

Each spec requirement maps to a concrete design element; tests listed here are the essential set (no token-library tests).

| Requirement | Design element | Verified by |
|---|---|---|
| FR-001, FR-002, FR-004 | `SessionService.CreateAsync(LoginOperationResult)`; `Success` factory internal; eligibility re-check without password | Test: login creates session for correct user/app; ineligible entity between authentication and creation yields no session (service-level test resolving the real services from the DI container) |
| FR-003, FR-005, FR-006 | `Session` entity, `SessionPolicy`, `GetState` | Domain tests: expiry arithmetic, state table, revoke idempotence, invalid lifetime/identifiers rejected; startup test: zero/negative lifetime fails |
| FR-007, FR-008, FR-009 | Issuer/claim set, `exp = min(...)` | Integration: claims limited to the six names; credential `exp <= sessionExpiresAt`, including near session end |
| FR-010, FR-011, FR-012 | `SessionService.ValidateAsync`, uniform `401` | Integration: valid, tampered, wrong-algorithm, expired (fake clock), revoked, wrong-application all reject with identical response bodies |
| FR-013, FR-014 | Revoke + logout, durable `RevokedAt` | Integration: logout then validate/renew rejected; second session unaffected; persisted `RevokedAt` read from a fresh scope |
| FR-015, FR-016 | Independent sessions; composite membership FK; `aud` and caller-application comparison | Integration: two sessions same app plus one other app; session for A rejected for B even with membership in both |
| FR-017, FR-018 | Eligibility checks at create and at every authoritative check; no revoke on deactivation | Integration: inactive user/application/membership refuse login-session creation; existing session rejected while inactive and accepted after reactivation |
| FR-019 | No roles/permissions in claims | Integration: decoded claim names are exactly `iss`, `sub`, `aud`, `sid`, `iat`, `exp` |
| FR-020 | `renew` with authoritative check | Integration: renew succeeds with same `sid` and bounded `exp`; fails for expired or revoked |
| FR-021, FR-023 | Uniform `401`; `signing-keys` and `session-credentials` rate-limit policies; `SafeExceptionHandler` for unexpected failures | Integration: expected failures never `500`; an infrastructure failure during validation returns the safe `500` and never grants access; keys and credential routes return `429` beyond their limits |
| FR-022 | External key, ES256, `ECDsa` | Startup test: missing key outside Development fails generically; no key in logs/responses |
| FR-024 | `ISecurityEventRecorder` with `sessionId`; structured logs | Integration: recording recorder sees `SessionCreated`, `SessionRevoked`, `LogoutCompleted`, `AccessRejected*`; captured logs contain no credential substring |
| FR-025 | `Sessions` table, migration | Migration test: `Sessions` table, FK, and index exist after `MigrateAsync` |
| FR-026, FR-027 | Ports + Infrastructure adapter; architecture test | Architecture test: Domain/Application reference no `Microsoft.IdentityModel`/`System.IdentityModel` assembly |
| FR-028 | Scope boundaries | Review: no refresh, OAuth/OIDC, MFA, recovery, rotation, or cache code added |

## Complexity Tracking

| Item | Why needed | Simpler alternative rejected because |
|---|---|---|
| New package `Microsoft.IdentityModel.JsonWebTokens` (Infrastructure only) | The clarified decision requires a signed token verifiable locally by consumers; the .NET shared framework has no JWS/JWT implementation | A hand-rolled envelope is prohibited by the spec and risks algorithm-confusion and `alg=none` flaws; the middleware package (`JwtBearer`) adds a larger surface and a second validation path that would bypass the Application-layer session checks |
| New anonymous route `GET /auth/signing-keys` and two new rate-limit policies (`signing-keys`, `session-credentials`) | Local signature verification is impossible without a way to obtain the public key; the constitution requires rate limiting on abuse-prone public and credential endpoints | Out-of-band key copying is undocumented and brittle; leaving credential routes unlimited would violate the security constraints. The credential-route default is generous so consuming APIs are unaffected |
