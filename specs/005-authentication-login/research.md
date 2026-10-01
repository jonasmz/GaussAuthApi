# Research: Application-Scoped Authentication Login

## Credential verification mechanism

**Decision**: Register `SignInManager<IdentityUser<Guid>>` alongside the already-registered `UserManager<IdentityUser<Guid>>` (via `.AddSignInManager()` on the existing `AddIdentityCore<IdentityUser<Guid>>()` chain in `InfrastructureServiceCollectionExtensions`), and verify credentials through `SignInManager.CheckPasswordSignInAsync(identityUser, password, lockoutOnFailure: true)`. This single call both validates the password against Identity's existing hash and participates in Identity's built-in lockout counters (incrementing `AccessFailedCount` on failure, resetting it on success, and consulting `LockoutEnd`), without any custom hashing, comparison, or lockout bookkeeping.

**Rationale**: `CheckPasswordSignInAsync` is the standard ASP.NET Core Identity primitive for exactly this orchestration (credential check + lockout), reusing the project's existing `IdentityUser<Guid>` row (already created by `IdentityCredentialProvisioningService` with `Id == domain User.Id`) without introducing a parallel password-check path. It keeps password hashing and lockout state entirely inside Identity/Infrastructure, satisfying the constitutional requirement that the Domain/Application layers never touch password hashes or Identity internals.

**Alternatives considered**: Calling `UserManager.CheckPasswordAsync` directly and manually incrementing/resetting `UserManager.AccessFailedCountAsync`/`ResetAccessFailedCountAsync` was rejected as unnecessary reinvention of behavior `SignInManager` already provides correctly, and risks subtly diverging from Identity's own lockout semantics (e.g., respecting `Lockout.AllowedForNewUsers`).

## Credential verification port shape

**Decision**: Introduce one minimal, focused Application-layer port, `ICredentialVerificationService`, with a single method `VerifyPasswordAsync(Guid userId, string password, CancellationToken)` returning a small closed result (`Success`, `InvalidPassword`, `LockedOut`). Infrastructure implements it (`IdentityCredentialVerificationService`) using `UserManager.FindByIdAsync` + `SignInManager.CheckPasswordSignInAsync` as described above.

**Rationale**: Applies Interface Segregation and YAGNI as the spec requires: the login use case only ever needs "is this password valid for this already-resolved user, and is the account currently locked," nothing broader. This port is intentionally separate from the existing `ICredentialProvisioningService` (used only for account creation) rather than widening that interface, since the two operations have unrelated callers and unrelated failure shapes.

**Alternatives considered**: A single generic `IIdentityService` covering provisioning, verification, and future password-change/reset operations was rejected — the constitution and spec explicitly warn against a broad generic identity abstraction containing unrelated future operations.

## Email resolution for login

**Decision**: Reuse `ICredentialProvisioningService.NormalizeEmail(string)` (already implemented via `ILookupNormalizer`) to normalize the submitted login email, then add one new read method to the existing `IUserRepository` port, `GetByNormalizedEmailAsync(string normalizedEmail, CancellationToken)`, implemented in Infrastructure against the existing `Users` table/normalized-email index.

**Rationale**: `002-users-profiles` already established and persisted normalized-email semantics and uniqueness; reusing the same normalization function guarantees the login flow resolves identity exactly the way creation/uniqueness already does, with no new normalization rule (per spec FR-002 and Assumptions).

**Alternatives considered**: Normalizing email independently inside the new login service was rejected as a duplicate, potentially-diverging implementation of a rule that must stay single-sourced.

## Application and membership resolution

**Decision**: Resolve the target application by its stable code using the existing `IApplicationRepository.GetByCodeAsync(string, CancellationToken)`, then resolve the membership using the existing `IApplicationMembershipRepository.GetAsync(userId, applicationId, CancellationToken)`. No new ports are required for this feature.

**Rationale**: Both ports already exist from `003-applications-memberships` and already enforce the same active-state and isolation semantics this feature must respect; reusing them keeps authentication consistent with the established application/membership model instead of re-deriving it.

**Alternatives considered**: None; this is a direct reuse of existing, already-tested infrastructure.

## Uniform failure response

**Decision**: The `LoginService` use case returns a closed `LoginOperationResult` with only two externally meaningful states: `Success(userId, applicationId)` and `Failure` (no reason code exposed in the type's public surface beyond an internal-only reason used solely for logging/security-event purposes). The API endpoint maps `Failure` to a single Problem Details response (`401 Unauthorized`, generic title, no body detail distinguishing cause) for every case reached through the use case — unknown email, wrong password, inactive user, invalid/inactive application, missing/inactive membership, and account lockout — per the 2026-10-01 clarification. Malformed input (missing/invalid fields) is rejected by request validation before the use case runs and uses the project's standard `400` Problem Details shape, which is not a credential/account-state outcome and is therefore not required to be indistinguishable from the `401` outcome.

**Rationale**: Directly implements FR-007 and the clarification recorded in the spec. Keeping the internal reason out of the result type's public surface (rather than just "don't serialize it") prevents an API author from accidentally leaking it later.

**Alternatives considered**: Returning a typed enum of failure reasons from the service and trusting the endpoint to collapse them was considered, but risks future endpoint code accidentally branching on the reason and leaking it; keeping the reason internal-only (logged at the point of decision, not threaded through the public result) removes that risk by construction.

## Rate limiting policy

**Decision**: Add a second named ASP.NET Core rate-limiting policy, `"login"`, alongside the existing `"user-creation"` policy in `ApiServiceCollectionExtensions`, using the same `FixedWindowLimiter` partitioned by `RemoteIpAddress`, with its own configuration keys (`RateLimiting:Login:PermitLimit`, default 5; `RateLimiting:Login:WindowSeconds`, default 60) and its own rejection handling (`429 Too Many Requests` with `Retry-After`, per the existing `OnRejected` pattern already shared globally).

**Rationale**: Reuses the exact pattern already established and tested for `/users` creation, satisfying the constitutional preference for native ASP.NET Core capabilities and avoiding a new rate-limiting mechanism. Partitioning by IP (not by submitted email) keeps this layer's responsibility strictly at the request/transport level, cleanly separate from Identity's per-account lockout, per the 2026-10-01 clarification and the spec's "Interaction between rate limiting and lockout" section.

**Alternatives considered**: Partitioning by submitted email was rejected — it would require resolving identity before rate limiting runs (defeating the "reject before credential validation" requirement, FR-012) and would let an attacker trivially bypass the limiter by rotating emails while still hammering the endpoint from one source.

## Security event recording

**Decision**: Introduce one minimal Application-layer port, `ISecurityEventRecorder`, with a single method `RecordAsync(SecurityEventType type, Guid? userId, Guid? applicationId, CancellationToken)`, where `SecurityEventType` is a small enum (`LoginSucceeded`, `LoginFailed`, `AccountLockedOut`). Infrastructure implements it with one adapter, `LoggingSecurityEventRecorder`, that writes one structured log entry per call (category/event id, optional user id, optional application id, timestamp — supplied by the logging framework — no password or credential content). No new database table or domain entity is introduced.

**Rationale**: Satisfies the spec's explicit instruction to implement only the minimum port/event mechanism now, while remaining compatible with `009-security-audit` later swapping in a persisted `SecurityEvent` adapter behind the same port without changing the Application layer. Logging-only avoids a schema change this feature does not need (constitution: no new business-domain persistence entities without necessity; Simplicity/YAGNI).

**Alternatives considered**: Adding a `SecurityEvents` table and EF migration now was rejected as premature — the spec explicitly defers the complete audit subsystem to `009-security-audit`, and the constitution warns against speculative infrastructure.

## Request contract and route

**Decision**: Expose `POST /auth/login` accepting `{ "applicationCode": string, "email": string, "password": string }` and returning `200` with `{ "userId": guid, "applicationId": guid }` on success, or a `401` Problem Details on any credential/account-state failure, or `400` Problem Details on invalid input, or `429` on rate-limit rejection (no body distinguishing cause beyond the standard `Retry-After` header already used elsewhere).

**Rationale**: Matches the conceptual route from the spec and the 2026-10-01 clarification (application identified by code only). Field bounds mirror existing precedent: email `MaxLength(256)` + `[EmailAddress]` (as in `CreateUserRequest`), password `MaxLength(128)` (as in `CreateUserRequest`, without a `MinLength` here since this is a login check, not a policy-setting input), application code `MaxLength(64)` (as in `CreateApplicationRequest`).

**Alternatives considered**: Returning the full `User`/`Application` resource shapes on success was rejected — the spec requires only the minimal stable identifiers needed by `006-sessions-access`, and returning more would both leak unnecessary data and risk coupling this feature's contract to those resources' evolution.

## Lockout configuration

**Decision**: Configure `IdentityOptions.Lockout` inside the existing `AddIdentityCore<IdentityUser<Guid>>()` options callback, reading `Identity:Lockout:MaxFailedAccessAttempts` (default `5`, Identity's own built-in default) and `Identity:Lockout:DefaultLockoutMinutes` (default `5`, Identity's own built-in default) from configuration, with `Lockout.AllowedForNewUsers = true`.

**Rationale**: Uses Identity's own existing default values as the fallback (not an invented threshold) while making both configurable, satisfying the constitutional requirement that exact thresholds/durations be configurable and defined at the feature/plan level rather than hard-coded.

**Alternatives considered**: Leaving Identity's defaults unconfigured (not reading from configuration at all) was rejected because the constitution and spec both explicitly require lockout configuration to be externally configurable, not merely whatever Identity ships with.
