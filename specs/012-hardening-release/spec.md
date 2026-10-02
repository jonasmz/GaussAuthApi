# Feature Specification: Hardening and Release Readiness

**Feature Branch**: `012-hardening-release`

**Created**: 2026-10-02

**Status**: Draft

**Input**: User description: "Create feature `012-hardening-release` for the reusable generic Authentication and Authorization API: final production-readiness hardening, release validation, deployment preparation, and operational documentation, building on features 001–011, without redesigning the platform or adding new functional capabilities unless a concrete release-blocking defect requires it."

## Context

Features 001–011 delivered the complete functional platform: users and profiles, applications and memberships, roles and permissions, login, sessions, password management, the authorization contract for consuming APIs, security audit, profile files, and administrative operations. This feature adds no new business capability. It verifies that the assembled system is coherent, reproducible, deployable, observable, secure by default, and documented well enough for an operator who did not build it to run it and for consuming applications to rely on it.

Where verification reveals a defect, the defect is classified. A defect that compromises authentication correctness, authorization isolation, session revocation, credential secrecy, consumer-secret isolation, password/reset secrecy, audit integrity, database migration consistency, startup reliability, or production configuration safety is **release-blocking** and MUST be corrected in this feature. All other findings are corrected when low-risk, or recorded as documented operational limitations.

## Clarifications

### Session 2026-10-02

- Q: How should operators apply database migrations in production, given that the running service will not apply them on its own? → A: Both. The documented default is a one-off migration command run from the same production image before the new version starts; each release also provides an SQL script of the same migrations for environments where operators or DBAs must review and apply changes manually. The script is an operational alternative, not a separate schema-evolution mechanism.
- Q: Who should be allowed to call the health and readiness endpoints in production? → A: Any caller that can reach the service, without authentication, receiving only minimal operational status. No probe-specific credentials are introduced. Network exposure is controlled by the deployment (network, reverse proxy, firewall, ingress, or equivalent), and any future detailed diagnostics view is a separate, explicitly protected capability.
- Q: When the service runs in production but no trusted proxy has been configured, should it start normally, start with a warning, or refuse to start? → A: Start normally, ignore all forwarded headers (behaving as a direct deployment), and log a clear startup warning. Forwarded headers are processed only when trusted proxies or networks are explicitly configured; an invalid or unsafe proxy configuration fails configuration validation.
- Q: In production, should the service refuse to start if profile image storage is not on persistent storage, or only warn? → A: Production configuration must explicitly declare whether profile storage is persistent. A missing declaration rejects startup; a declaration of persistent starts normally; an explicit declaration of non-persistent starts with a clear, visible warning that images may be lost when the container is replaced or recreated. The service never infers persistence from the path. The check applies only in Production unless another setting explicitly enables it elsewhere.
- Q: How should password recovery behave in production, given the service has no production way to deliver reset credentials (the only adapter is file-based and works only in Development and Testing)? → A: Recovery delivery is an operator-supplied extension. With no production delivery adapter configured, the service starts normally and emits a clear structured warning; recovery requests keep returning the same generic response for existing and unknown accounts; no reset credential is generated, logged, audited, or returned when no delivery channel exists; the public recovery contract does not change; and a future production adapter (SMTP, webhook, or other) can be added through the existing delivery interface without redesigning the flow. No SMTP, webhook, or other delivery mechanism is added in this feature, and the limitation is documented.
- Q (release revision after planning): Should persistence of the protected-state key ring be optional or mandatory in Production? → A: Mandatory in Production-class environments (optional in Development and Testing). Production startup fails clearly, naming only the missing or invalid configuration key and never exposing key material, when no valid key-ring location is configured. The location must survive application/container recreation; the service cannot verify persistence from the path and the operator is responsible (documented), consistent with the profile-storage decision. Persistence is required to preserve password-reset credentials and any other protected state across restarts and replicas.
- Q (release revision after planning): How is the first global administrator provisioned, given user creation itself requires a global administrator? → A: A one-off operator command shipped in the same image (like the migration command) creates the first user through the existing user-creation rules and prints the new UserId. It refuses to run if any user already exists, takes credentials from the environment or a mounted secret (never command-line arguments, never printed), and grants no authority by itself: global authority exists only when that UserId is listed in the externally configured global administrator list (decision of feature 011 preserved). An empty list means no global authority. This is recorded as a correction of a release-blocking startup-reliability defect (the platform otherwise cannot be bootstrapped), not a new product capability.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Safe and Predictable Startup (Priority: P1)

An operator starts the service in a Production environment. If any critical configuration is missing or invalid, the service refuses to start and states clearly which setting is wrong, without printing any secret value. If everything is valid, it starts only in a secure configuration and never silently falls back to a development convenience.

**Why this priority**: A service that starts in an insecure or half-configured state is the most dangerous release failure, and every other operational guarantee depends on a trustworthy startup.

**Independent Test**: Start the service in Production with each critical setting removed or invalid in turn (database connection, session signing material, file-storage root, rate-limit and lifetime values, global administrator list format). Confirm each case fails fast with a safe, specific message and that no secret value appears in output. Then start with a complete valid configuration and confirm it starts.

**Acceptance Scenarios**:

1. **Given** Production with a missing critical setting, **When** the service starts, **Then** it fails before accepting requests, naming the setting and not its value.
2. **Given** Production with an invalid value (non-positive lifetime, impossible rate limit, unusable storage path, malformed signing material), **When** the service starts, **Then** it fails with an actionable message.
3. **Given** Production without externally supplied signing material, **When** the service starts, **Then** it does not generate or fall back to an ephemeral or development key and fails instead.
4. **Given** Production, **When** the service starts, **Then** development-only behaviors (fake or file-based recovery delivery, development key generation, verbose error detail, permissive bootstrap) are not active unless explicitly and visibly enabled.
3a. **Given** Production without a valid persistent key-ring location configured for protected state, **When** the service starts, **Then** startup is refused with a message naming only the missing or invalid setting; Development and Testing may start without it.
4a. **Given** Production with no password-recovery delivery adapter configured, **When** the service starts, **Then** it starts normally and emits a clear structured warning that password recovery delivery is not configured.
4b. **Given** that same state, **When** a recovery request is made for an existing account and for an unknown account, **Then** both receive the same generic response, no reset credential is generated, and no reset credential appears in any log, audit event, or response.
5. **Given** a valid Production configuration, **When** the service starts, **Then** the startup is logged with environment and non-sensitive configuration summary, and shutdown is logged.
6. **Given** Development and Testing environments, **When** the service starts, **Then** development conveniences remain available there and are clearly separated from Production behavior.

---

### User Story 2 - Reproducible Database Creation and Upgrade (Priority: P1)

An operator creates a brand-new database from zero using the full migration chain, and upgrades an existing database from the previous release state, with a documented, explicit procedure and no silent data loss.

**Why this priority**: Migration inconsistency is release-blocking, and a deployment that cannot create or upgrade its schema reproducibly cannot be released.

**Independent Test**: Against an empty PostgreSQL 17 database apply every migration in order, start the service against the resulting schema, and verify critical constraints and indexes exist. Separately apply the final migrations to a database populated at the previous release state with representative data, and verify no user, security, authorization, or audit data is lost or silently altered.

**Acceptance Scenarios**:

1. **Given** an empty database, **When** all migrations are applied, **Then** they succeed in order and the service starts and serves requests against the schema.
2. **Given** the resulting schema, **When** inspected, **Then** the constraints and indexes that enforce email uniqueness, application-scoped uniqueness, membership uniqueness, and cross-application integrity exist.
3. **Given** a database at the previous schema state with representative data, **When** upgraded, **Then** existing users, memberships, roles, assignments, sessions, security events, and consumer credentials are preserved or transformed only as documented.
4. **Given** the migration set, **When** reviewed, **Then** any operation that drops, rewrites, or invalidates user, security, authorization, or audit data is intentional and documented; none is silent.
5. **Given** a migration that fails, **When** it fails, **Then** the diagnostics identify the failing migration and cause without exposing credentials.
6. **Given** the deployment documentation, **When** read, **Then** it presents the one-off migration command run from the production image as the standard path, the release SQL script as the alternative for manual review and application, and states that the running service never applies migrations on startup.
7. **Given** a release, **When** its migration command and its SQL script are compared, **Then** both represent exactly the same set of migrations and produce the same final schema from the same starting state.
8. **Given** a deployment, **When** the migration command fails, **Then** the deployment stops and the new version is not started against an incompatible schema.

---

### User Story 3 - Health and Readiness Reporting (Priority: P1)

An orchestrator or operator can ask whether the process is alive and whether it can safely serve requests, and gets a minimal answer that reveals nothing sensitive.

**Why this priority**: Deployments, restarts, and load balancing depend on trustworthy liveness and readiness signals; hiding a database outage behind a healthy status is itself a release-blocking reliability defect.

**Independent Test**: With dependencies healthy, query liveness and readiness and observe success. Stop the database and confirm liveness still succeeds while readiness reports not-ready. Make profile storage unusable and confirm readiness reports not-ready. Inspect all responses for sensitive data.

**Acceptance Scenarios**:

1. **Given** a running process, **When** liveness is queried, **Then** it reports alive and does not depend on any external system.
2. **Given** all critical dependencies available, **When** readiness is queried, **Then** it reports ready.
3. **Given** the database is unreachable, **When** readiness is queried, **Then** it reports not-ready; it is never reported ready.
4. **Given** the profile storage location is unusable, **When** readiness is queried, **Then** it reports not-ready, because required functionality cannot operate.
5. **Given** any health response in any state, **When** inspected, **Then** it contains no connection string, credential, database internals, configuration values, environment details, or stack trace.
6. **Given** health endpoints, **When** called without credentials, **Then** they return only minimal status and no detailed diagnostics; no authentication or probe-specific credential is required.
7. **Given** a not-ready result caused by an unavailable dependency, **When** the response is inspected, **Then** it indicates not-ready without revealing which dependency failed or why.
8. **Given** the deployment documentation, **When** read, **Then** it states that health endpoints should not be published to the Internet unnecessarily and that their exposure must be controlled by network, reverse proxy, firewall, ingress, or an equivalent mechanism.

---

### User Story 4 - Verified Release Build and Full Regression (Priority: P1)

A release engineer produces a Release build from a clean checkout and runs the essential regression suite, with confidence that critical behavior across all eleven prior features still holds.

**Why this priority**: This is the release gate. It proves the assembled system, not individual features in isolation, is correct.

**Independent Test**: From a clean checkout, restore, build in Release configuration, and run the full test suite against a clean database. All pass. Review warnings for correctness, security, nullability, resource-lifetime, and API-misuse categories.

**Acceptance Scenarios**:

1. **Given** a clean checkout, **When** the solution is restored and built in Release, **Then** it succeeds with no unreviewed correctness, security, nullability, resource-lifetime, or API-misuse warnings.
2. **Given** the Release build, **When** the full essential suite runs, **Then** it passes, covering authentication, application isolation, role/permission isolation, session expiration and revocation, password management, consumer authentication, authorization contract, audit, profile file security, and administrative authorization.
3. **Given** a regression discovered during review, **When** it affects a release-blocking category, **Then** it is fixed with a regression test and not deferred as documentation.
4. **Given** the cross-feature consistency review, **When** completed, **Then** each area listed in the review checklist (users, applications/memberships, roles/permissions, login, sessions, password management, authorization contract, security/audit, profile files, admin operations) has recorded evidence of verification.

---

### User Story 5 - Deployable Runtime (Priority: P2)

An operator deploys the service from a documented, reproducible runtime package, supplying all secrets and environment-specific settings externally, with profile storage that survives container replacement.

**Why this priority**: Deployability makes the system reusable by others, but it builds on the correctness and startup guarantees above.

**Independent Test**: Build the production image, run it with a database and a persistent storage volume using only externally supplied configuration, upload a profile image, replace the container, and confirm the image is still available and the service reports ready.

**Acceptance Scenarios**:

1. **Given** the repository, **When** the production image is built, **Then** it is reproducible, runs as a non-privileged user, and contains no secrets, development tooling, or source of test credentials.
2. **Given** the production image, **When** run, **Then** all secrets and environment-specific settings are supplied externally and none are baked in.
3. **Given** profile storage configured on a persistent volume, **When** the container is replaced, **Then** previously uploaded profile files remain available.
4. **Given** Production with no declaration of whether profile storage is persistent, **When** the service starts, **Then** startup is rejected with a message naming the missing declaration.
4a. **Given** Production with storage explicitly declared persistent, **When** the service starts, **Then** it starts normally.
4b. **Given** Production with storage explicitly declared not persistent, **When** the service starts, **Then** it starts and emits a clear, visible warning that profile images may be lost when the container is replaced or recreated.
5. **Given** a local/integration composition for evaluation, **When** started, **Then** the service, database, and persistent storage run together without altering the existing development container workflow.
6. **Given** a termination signal, **When** the service shuts down, **Then** in-flight requests complete or are cancelled cleanly and no persisted state is corrupted.

---

### User Story 6 - Secure-by-Default HTTP and Proxy Behavior (Priority: P2)

A security reviewer confirms that the service's network-facing behavior is safe in production: transport expectations, forwarded-header trust, response headers, error responses, request limits, and rate limits are all deliberate and documented.

**Why this priority**: These controls protect every consumer and are the last line of defense, but most are verifications of existing behavior plus targeted tightening.

**Independent Test**: Exercise error paths, oversized requests, over-limit upload attempts, and rate-limit thresholds in a Production-like configuration; send forged forwarding headers from an untrusted source; inspect response headers.

**Acceptance Scenarios**:

1. **Given** Production, **When** a request fails unexpectedly, **Then** the response is a stable, generic error contract exposing no stack trace, file path, query text, framework internals, identity internals, cryptographic detail, or raw exception.
2. **Given** a request arriving with forwarding headers from a source that is not a configured trusted proxy, **When** processed, **Then** the forwarded values are ignored.
2a. **Given** no trusted proxy or network is configured, **When** the service starts, **Then** it starts normally as a direct deployment, ignores all forwarded headers, and logs a clear warning stating that forwarded headers will be ignored and that, if the service is actually behind a reverse proxy, the remote address, HTTPS detection, and any policy depending on them may not represent the real client.
2b. **Given** a trusted-proxy configuration that is invalid or unsafe (for example, one that would trust any source), **When** the service starts, **Then** configuration validation fails.
3. **Given** a request arriving through a configured trusted proxy, **When** processed, **Then** the original scheme and client address are used for HTTPS detection and rate-limit partitioning.
4. **Given** Production responses, **When** inspected, **Then** content-type-sniffing protection is present, transport-security policy is applied where HTTPS is expected, and server implementation details are not unnecessarily disclosed.
5. **Given** the current server-to-server usage, **When** cross-origin browser requests arrive, **Then** none are permitted unless an explicit origin allowlist is configured; there is no allow-any-origin behavior.
6. **Given** oversized request bodies or uploads, **When** submitted, **Then** they are rejected at the configured limits.
7. **Given** the sensitive endpoint groups (login, recovery request, password reset, consumer authorization/introspection, file upload, administration), **When** reviewed, **Then** each has an appropriate, separately justified protection limit, and exceeding it is rejected without affecting other groups.

---

### User Story 7 - Operational Documentation and Secret Hygiene (Priority: P2)

An operator who did not build the system can deploy, configure, upgrade, back up, restore, and troubleshoot it from the documentation alone, and a reviewer can confirm no secret is in the repository.

**Why this priority**: Documentation is the deliverable that makes the platform reusable, and secret hygiene is a final security gate.

**Independent Test**: Follow the documentation on a clean machine to deploy, configure, and upgrade. Scan the repository and its history for real secrets. Confirm every configuration setting, its meaning, its default, and whether it is required in Production is documented.

**Acceptance Scenarios**:

1. **Given** the repository, **When** scanned, **Then** it contains no real database passwords, consumer secrets, signing keys, private keys, reset credentials, or SMTP/API credentials; only clearly fake placeholders appear in examples.
2. **Given** the documentation, **When** read, **Then** it covers deployment requirements, the complete configuration reference, secret injection, migration procedure, upgrade procedure, backup and restore (database and profile storage, including consistency between them), HTTPS and reverse-proxy expectations, health endpoints, logging and correlation, graceful shutdown, and known operational limitations.
3. **Given** the profile-storage section, **When** read, **Then** it states the required storage root, filesystem permissions, persistence and backup requirements, container volume behavior, and behavior when storage is unavailable.
4. **Given** a consumer-secret rotation, **When** performed per the documentation, **Then** consuming applications continue to validate during the retirement window.
4a. **Given** the bootstrap documentation, **When** followed on an empty database, **Then** an operator can create the first user with the one-off command, list its UserId in the global administrator configuration, restart, and perform global administrative operations; the documentation states that an empty list means no global authority.
4b. **Given** the documentation on signing keys, **When** read, **Then** it identifies the access-credential signing key as an ECDSA key on the NIST P-256 curve and keeps it conceptually separate from the symmetric, per-Application consumer secrets.
4c. **Given** the documentation on protected-state keys, **When** read, **Then** it states that persisting the key ring is required to preserve password-reset credentials and any other protected state across restarts and replicas.
5. **Given** the documentation, **When** a limitation is listed, **Then** it is not a release-blocking defect.

---

### User Story 8 - Observable Operation Without Sensitive Leakage (Priority: P3)

An operator diagnosing a production incident can follow a request or security event through logs using existing correlation identifiers, and can see startup, configuration, database, and unexpected-failure events, without any sensitive value ever appearing.

**Why this priority**: Observability supports operations after release, building on the structured logging and correlation already established in 009.

**Independent Test**: Trigger configuration, database, security-sensitive, and unexpected-failure scenarios; inspect logs and security events for presence of meaningful entries and absence of tokens, passwords, secrets, reset material, file content, full authorization headers, and sensitive request bodies.

**Acceptance Scenarios**:

1. **Given** Production defaults, **When** the service runs, **Then** log verbosity avoids debug-level and sensitive output.
2. **Given** a failing request or security event, **When** investigated, **Then** the same correlation identifier links the log entries and the persisted security event.
3. **Given** all log output across the full regression suite, **When** searched, **Then** no access token, consumer secret, password, reset token, file content, or complete authorization header appears.
4. **Given** a database connectivity failure or migration failure, **When** it occurs, **Then** a meaningful log entry exists without credentials.

---

### Edge Cases

- The database is reachable at startup but becomes unavailable later: liveness remains healthy, readiness becomes not-ready, and recovery returns readiness to ready without restart.
- The database schema is behind the application version: the service reports a clear, actionable condition rather than failing with opaque data errors, and does not silently alter the schema.
- Configuration provides the same setting through multiple sources: precedence is documented and a Production-unsafe value cannot be silently selected by a lower-precedence default.
- Signing material is present but too weak or malformed: startup fails rather than accepting it.
- A secret rotation or administrative change occurs during shutdown: the change either completes fully or not at all.
- The global administrator list is empty in Production: that means no global authority (feature 011 decision preserved); no implicit administrator exists. A malformed list fails startup. The first administrator is provisioned with the documented one-off bootstrap command followed by listing its UserId.
- The bootstrap command is run when any user already exists: it refuses and changes nothing.
- No trusted proxy is configured in Production while actually behind a proxy: the service starts as a direct deployment, ignores forwarded headers, and logs a clear warning; the resulting client-address and HTTPS-detection limits are documented rather than trusting arbitrary headers.
- Profile storage is available at startup but fills or becomes read-only: upload fails safely with a stable error, no partial file remains, and readiness reflects it.
- A review finding sits on the boundary between release-blocking and a documented limitation: it is treated as release-blocking when it affects any listed protected category.

## Requirements *(mandatory)*

### Functional Requirements

**Release validation**

- **FR-001**: The complete solution MUST restore and build in Release configuration from a clean checkout without errors.
- **FR-002**: Build warnings indicating correctness, security, nullability, resource-lifetime, or API-misuse issues MUST be reviewed; each MUST be fixed or have a recorded justification.
- **FR-003**: The essential regression suite MUST pass in Release configuration against a clean database and MUST cover authentication, application isolation, role/permission isolation, session expiration/revocation, password management, consumer authentication, authorization contract, audit, profile file security, and administrative authorization.
- **FR-004**: A recorded consistency review MUST confirm, with evidence, the behaviors listed for users, applications/memberships, roles/permissions, login, sessions, password management, authorization contract, security/audit, profile files, and administrative operations.
- **FR-005**: Any defect found affecting authentication correctness, authorization isolation, session revocation, credential secrecy, consumer-secret isolation, password/reset secrecy, audit integrity, migration consistency, startup reliability, or production configuration safety MUST be fixed in this feature with a regression test, and MUST NOT be deferred as a documented limitation.
- **FR-006**: No new functional capability, endpoint family, or authorization concept MAY be added except as the minimal correction of a release-blocking defect or the health endpoints required by this feature.

**Database and migrations**

- **FR-007**: The full migration chain MUST create a working schema from an empty PostgreSQL 17 database, and the service MUST start and operate against it.
- **FR-008**: A validation MUST prove that critical constraints and indexes exist after migration from zero.
- **FR-009**: Migration from the previous release schema state to the final state MUST be validated with representative data wherever practical, and any data transformation MUST be documented.
- **FR-010**: All migrations MUST be reviewed for destructive operations; any operation affecting user, security, authorization-history, or audit data MUST be intentional and documented, and none MAY be silent.
- **FR-011**: Migration failure MUST produce diagnostics that identify the failing step and cause without exposing credentials.
- **FR-012**: The running service MUST NOT apply migrations automatically at startup in any Production deployment. Production migrations MUST be applied by an explicit deployment step documented for operators.
- **FR-012a**: The standard migration mechanism MUST be a one-off command executed from the same production image as the release, so that migration logic and migrations are exactly the version being released, and MUST NOT require the .NET SDK or any development tooling on the production host.
- **FR-012b**: Each release MUST provide an SQL script representing exactly the migrations included in that release, as an operational alternative for environments where operators or DBAs review and apply changes manually. It MUST NOT constitute a second, divergent schema-evolution mechanism.
- **FR-012c**: The documented deployment procedure MUST run the migration first and start the new version only if the migration completes successfully; a migration failure MUST stop the deployment and MUST NOT result in a version running against an incompatible schema.
- **FR-012d**: Neither the production image nor the SQL script MAY embed database credentials; credentials MUST be supplied externally at execution time.
- **FR-013**: If the database schema does not match the version the service requires, readiness MUST report not-ready and logs MUST state the mismatch actionably.

**Startup and configuration**

- **FR-014**: In Production the service MUST validate all critical configuration at startup and refuse to start when any required setting is missing or invalid, before accepting requests.
- **FR-015**: Configuration validation MUST cover at least the database connection, session signing material, session and token lifetimes, consumer-secret related settings, profile-storage root, persistence declaration, protected-state key-ring location, and limits, rate-limit values, administrative limits, and the global administrator list format.
- **FR-016**: Startup failure messages MUST name the offending setting and the reason and MUST NOT include any secret value.
- **FR-017**: In Production the service MUST NOT generate, default, or fall back to an ephemeral or development signing key; signing material MUST be supplied externally and MUST meet documented strength requirements.
- **FR-018**: Development-only conveniences (non-production recovery delivery, development key handling, verbose error detail, test credentials, developer bootstrap, insecure transport assumptions) MUST NOT be active in Production unless explicitly and visibly enabled, and MUST remain available in Development and Testing.
- **FR-018a**: Password-recovery delivery MUST be an operator-supplied extension in Production. When no delivery adapter is configured, the service MUST start normally and emit a clear structured warning; recovery requests MUST return the same generic response for existing and unknown accounts (preserving enumeration resistance), MUST NOT fail differently depending on account existence, and MUST NOT generate any reset credential; no reset credential MAY appear in logs, audit events, or responses. The public recovery contract MUST NOT change, and a future production adapter MUST be addable through the existing delivery interface without redesigning the flow. This feature MUST NOT add SMTP, webhook, or any other delivery mechanism.
- **FR-019**: Environment-specific configuration MUST clearly separate Development, Testing, and Production, and the repository defaults MUST be safe for Production.
- **FR-020**: Token and session lifetimes MUST be explicitly configured, and the parameters used to validate credentials MUST match those used to issue them.
- **FR-021**: Startup and shutdown MUST be logged, and critical configuration errors, database connectivity failures, and migration failures MUST produce meaningful non-sensitive log entries.

**Secrets**

- **FR-022**: The repository MUST NOT contain real database passwords, consumer secrets, signing or private keys, reset credentials, or SMTP/API credentials; examples MUST use obviously fake placeholders.
- **FR-023**: Production secrets MUST be injectable externally (environment, mounted secrets, or platform secret mechanisms) without requiring any particular external secret-management product.
- **FR-024**: The consumer-secret model MUST be verified: each Application has independent credentials; stored forms are one-way verifiable; current/previous rotation semantics work; plaintext is available only at issuance or rotation; retired credentials stop working per policy; and secrets never appear in logs, audit events, or error responses.
- **FR-025**: Cryptographic and token mechanisms MUST use established platform mechanisms with adequate key strength and no newly invented cryptography.

**Health and readiness**

- **FR-026**: The service MUST expose a liveness signal that reports process responsiveness and does not depend on any external system.
- **FR-027**: The service MUST expose a readiness signal that reports not-ready when the database is unreachable, when the schema is incompatible, or when profile storage required for operation is unusable, and ready otherwise.
- **FR-028**: Health endpoints MUST NOT require authentication, and no probe-specific credentials MAY be introduced. Responses MUST be limited to minimal operational status and MUST NOT reveal server names, connection strings, credentials, internal versions, exceptions, configuration, environment details, stack traces, or the identity and internals of any dependency. Liveness MUST indicate only whether the process is operating; readiness MAY indicate unavailability of critical dependencies but MUST NOT reveal the internal cause.
- **FR-028a**: Any detailed diagnostics view, if ever provided, MUST be a separate capability that is explicitly protected and MUST NOT be enabled by default. Operator documentation MUST state that health endpoints are not intended for unnecessary Internet exposure and that their exposure is controlled by network, reverse proxy, firewall, ingress, or equivalent means.
- **FR-029**: Health signals MUST be excluded from, or separately limited by, request-protection rules so that orchestrators are not locked out and the signals cannot be used for abuse.

**HTTP and network security**

- **FR-030**: Production error responses MUST use a stable error contract and MUST NOT expose stack traces, file paths, SQL, ORM internals, identity-framework internals, cryptographic details, or raw exceptions; status codes MUST be appropriate to the failure.
- **FR-031**: Forwarded headers (scheme, client address) MUST be honored only from explicitly configured trusted proxies or networks and ignored otherwise; the service MUST NOT trust forwarded headers automatically and MUST NOT have any fallback that trusts any proxy. The behavior MUST be documented.
- **FR-031a**: When no trusted proxy or network is configured, the service MUST start normally, behave as a direct deployment, and log a clear startup warning that forwarded headers are ignored and that, behind a real reverse proxy, remote address, HTTPS detection, and dependent policies may not represent the real client.
- **FR-031b**: An invalid or unsafe trusted-proxy configuration (including one that would trust arbitrary sources) MUST fail configuration validation.
- **FR-032**: Production MUST apply transport-security policy where HTTPS is expected, MUST send content-type-sniffing protection, and SHOULD avoid disclosing server implementation details.
- **FR-033**: Cross-origin browser access MUST be denied by default; if enabled, it MUST use an explicit origin allowlist and MUST NOT permit any origin on authenticated endpoints.
- **FR-034**: Request-size and upload limits MUST be explicitly configured and enforced.
- **FR-035**: Rate-limit protection MUST be verified for login, recovery request, password reset, consumer authorization/introspection, profile upload, and administrative endpoints, each with limits suited to its abuse profile rather than one uniform limit, and the chosen values MUST be documented.

**Observability and shutdown**

- **FR-036**: Production log defaults MUST avoid debug-level and sensitive output; Development MAY be more verbose.
- **FR-037**: Logs and persisted security events MUST NOT contain access tokens, consumer secrets, passwords, reset tokens, file content, complete authorization headers, or sensitive request bodies.
- **FR-038**: Operational errors and security events MUST remain traceable through the existing correlation identifiers; no parallel tracing identity MAY be introduced.
- **FR-039**: Shutdown MUST respect cancellation so that in-flight operations complete or abort cleanly without leaving partial persisted state or partial files.

**Runtime and deployment**

- **FR-040**: A production runtime package MUST be reproducibly buildable, MUST run as a non-privileged user, and MUST NOT contain secrets, development tooling, or test credentials.
- **FR-041**: Production configuration MUST explicitly declare whether profile storage is persistent. Startup MUST be rejected when the declaration is absent; MUST proceed normally when declared persistent; and MUST proceed with a clear, visible warning (that images may be lost when the container is replaced or recreated) when declared not persistent. The service MUST NOT infer persistence from the storage path. The check MUST apply only in Production unless another setting explicitly enables it in other environments, and the decision MUST be documented in the deployment and configuration guide.
- **FR-041a**: In Production-class environments the service MUST be configured with a persistent key-ring location for ASP.NET Core Data Protection (protected state such as password-reset credentials); startup MUST be refused with a clear message naming only the missing or invalid setting, and no key material or secret MAY appear in the message. The location MUST be documented as having to survive application and container recreation; the service MUST NOT infer persistence from the path. The setting MAY remain optional in Development and Testing.
- **FR-042**: A simple local/integration composition MUST be provided for evaluating the deployed service with its database and persistent storage, and MUST NOT replace or alter the existing development container workflow.

**Documentation**

- **FR-043**: Operator documentation MUST cover deployment requirements, a complete configuration reference (meaning, default, Production requirement), secret injection, migration and upgrade procedure, backup and restore for both database and profile storage with consistency guidance, HTTPS and reverse-proxy expectations, health signals, logging and correlation, graceful shutdown, profile-storage operations, consumer-secret rotation, protected-state key-ring persistence, first-administrator bootstrap, and known operational limitations, explicitly including that password recovery delivery requires an operator-supplied adapter in Production.

### First Administrator Bootstrap

- **FR-044**: The release MUST provide a documented one-off operator command, shipped in the same production image as the service, that creates the first user using the existing user-creation rules (email normalization, password policy) and prints the new UserId. It MUST refuse to run and change nothing when any user already exists; MUST take credentials from the environment or a mounted secret and never from command-line arguments; MUST NOT print or log the password; MUST NOT expose any HTTP surface; and MUST NOT grant authority by itself.
- **FR-045**: Global authority MUST exist only for UserIds listed in the externally configured global administrator list; an empty list MUST mean no global authority (feature 011 decision preserved). The operator documentation MUST describe the full first-administrator procedure: create the user, list its UserId, restart, verify.
- **FR-046**: Documentation MUST identify the access-credential signing key as ECDSA using the NIST P-256 curve and MUST keep it conceptually separate from the symmetric per-Application consumer secrets.

### Key Entities *(include if feature involves data)*

- **Release Configuration**: The complete set of externally supplied settings and secrets required to run in an environment, with per-setting validity rules and Production requirements.
- **Health Status**: A minimal liveness or readiness result derived from process responsiveness and critical dependency availability.
- **Migration Chain**: The ordered, version-controlled set of schema changes from an empty database to the current schema, with documented data effects.
- **Release Validation Record**: Evidence that the build, regression suite, migration validation, consistency review, and secret scan were performed and their results.
- **Operational Documentation Set**: The operator-facing documents covering deployment, configuration, upgrade, backup/restore, security expectations, and limitations.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A Release build and the full essential regression suite complete with zero failing tests and zero unreviewed correctness, security, nullability, resource-lifetime, or API-misuse warnings.
- **SC-002**: A brand-new database is created from zero and a service started against it in a single documented procedure, with 100% of migrations applying in order and 100% of critical constraints and indexes verified present.
- **SC-003**: Upgrading a representative database from the previous release state loses zero user, security, authorization, or audit records and every data-affecting migration step is documented.
- **SC-004**: In 100% of tested invalid Production configurations the service refuses to start, identifies the offending setting, and prints zero secret values.
- **SC-005**: In 100% of tested Production starts without externally supplied signing material, the service refuses to start rather than using a generated or development key.
- **SC-006**: Liveness remains healthy and readiness becomes not-ready within 10 seconds of the database or required storage becoming unavailable, and returns to ready within 10 seconds of recovery.
- **SC-007**: Zero sensitive values (tokens, secrets, passwords, reset material, file content, full authorization headers, connection strings) appear in any health response, error response, log, or security event across the full regression run.
- **SC-008**: Zero release-blocking defects remain open; every finding is recorded as fixed, or classified as a non-blocking documented limitation.
- **SC-009**: An operator unfamiliar with the project can deploy, configure, upgrade, and perform a backup and restore using only the documentation, with every Production-required setting documented.
- **SC-010**: Profile files uploaded before a container replacement are retrievable after it in 100% of tested replacements when persistent storage is configured.
- **SC-011**: A repository scan finds zero real secrets in tracked files.
- **SC-012**: Forged forwarding headers from untrusted sources change neither the detected scheme nor the client address used for rate limiting in 100% of tested cases.
- **SC-013**: Following the documentation on an empty database, an operator creates the first administrator, enables global authority, and completes a global administrative operation without touching the database directly, in 100% of tested runs; the bootstrap command refuses (changing nothing) in 100% of tested runs where a user already exists.
- **SC-014**: In 100% of tested Production starts without a valid key-ring location the service refuses to start naming only the setting; and a password-reset credential issued before an application/container restart remains valid after it when the documented persistent location is used.

## Assumptions

- Docker is the intended deployment mechanism; a production image and a local/integration composition are provided, while orchestration platforms and cloud-specific tooling are out of scope.
- Production migrations are applied by an explicit deployment step (one-off command from the production image) run before the new release starts; the release SQL script is the documented alternative for manual review. The service does not apply migrations implicitly in Production.
- The service is currently used server-to-server by consuming APIs, so cross-origin browser access is denied by default and no CORS allowlist is enabled unless an operator configures one.
- TLS is typically terminated at a reverse proxy; the service trusts forwarded headers only from explicitly configured proxies or networks.
- Existing structured logging, correlation identifiers (009), and rate-limiting mechanisms are reused; no external observability platform or secret-management product is introduced.
- The "previous release state" for upgrade validation is the schema produced by migrations through feature 010, with feature 011 migrations as the upgrade under test, supplemented by the full chain from zero.
- Behaviors established in earlier specifications (session freshness policy from 006, global administrator model from 011, consumer-secret rotation from 008/009, profile image handling from 010) are authoritative; this feature verifies them and does not change them except to fix release-blocking defects.
- Detailed health diagnostics beyond minimal status are not required; if ever added, they are a separate, explicitly protected capability and are not enabled by default.
- Load, performance, and capacity testing, high availability, multi-region deployment, and automated key-rotation orchestration are out of scope.
- Production password-recovery delivery is intentionally not provided by this feature; it is an operator-supplied extension through the existing delivery interface and is documented as a known operational limitation.
- The one-off bootstrap command is a correction of a release-blocking startup-reliability defect (no supported way to create the first administrator), reusing existing user-creation rules; it is not a new user-facing capability and adds no endpoint.
- The existing AI development container and development compose workflow remain unchanged and are not the production image.
