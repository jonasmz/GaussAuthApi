# Research: Security Audit

## Decision: Immutable persisted SecurityEvents

**Rationale**: Current enum events are logging-only. Persist framework-independent immutable events with stable string type, outcome, UTC time, known identifiers, correlation, and bounded safe context.

**Alternatives considered**: Log-only records and arbitrary request JSON were rejected as non-queryable or unsafe.

## Decision: Central event catalog and reliability policy

**Rationale**: The catalog owns type, outcome, allowed context, and critical/operational classification. Critical lifecycle/privilege/administrative-revocation/secret changes share a transaction with their event where practical; login, rejection, session observation, recovery, and context events remain operational and safely observable if their write fails.

**Alternatives considered**: Per-handler policy, fail-everything, silent failures, brokers, and distributed transactions were rejected.

## Decision: Keyset audit queries with forced authorization scope

**Rationale**: `(OccurredAtUtc DESC, Id DESC)` cursors provide stable bounded pages. Application scope is injected into the predicate before access; global scope requires a valid user session and a separately configured reviewer identity.

**Alternatives considered**: Offset pagination, public audit access, and hard-coded or same-named Administrator roles were rejected.

## Decision: Identity PasswordHasher consumer-secret representations

**Rationale**: Replace `CurrentSecret`/`RetiringSecret` with salted, versioned `CurrentSecretHash`/`RetiringSecretHash`, preserving the HTTP header and active-plus-retiring rotation. Plaintext is unrecoverable after out-of-band provisioning.

**Alternatives considered**: Plaintext config, custom PBKDF2 formats, and HMAC/fingerprint schemes were rejected. Salted hashes cannot detect duplicate plaintext provisioning at startup; provisioning policy and cross-application tests enforce independence.

## Decision: API-relevant hardening only

**Rationale**: Add nosniff, no-referrer, built-in trace correlation, production HTTPS/HSTS guidance, request/body/filter bounds, and no-store for sensitive responses. Avoid browser-only CSP/frame headers and a separate tracing framework.

## Decision: Configured retention without automatic purge

**Rationale**: Retention is deployment-specific. Document an administrative future purge separately while normal flows stay append-only.
