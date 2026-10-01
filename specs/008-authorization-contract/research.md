# Research: Authorization Contract

## Decision: authoritative context resolution

**Rationale**: Feature 006 requires persisted session state for revocation and eligibility, and explicitly keeps roles/permissions out of the access credential. Auth must therefore resolve current context for every authoritative request; local signature verification alone is insufficient.

**Alternatives considered**: Token-only permission claims and a distributed authorization cache were rejected because they make revocation and permission changes stale.

## Decision: distinct configured credential per consumer application

**Rationale**: An externally configured current credential, plus an optional temporary retiring credential, is simple, binds the caller to one application, and enables rotation without changing HTTP contract or user sessions. The Infrastructure adapter uses platform constant-time comparison and validates that secrets are not shared across applications.

**Alternatives considered**: Mutual TLS and OAuth client credentials were explicitly rejected. Reusing a user access credential was rejected because it is not a consumer service credential.

## Decision: minimal dynamic contract

**Rationale**: Consumers need identifiers, expiration, active roles, and unique permission codes. They do not need profile data, persistence entities, Identity types, security stamps, or key material.

**Alternatives considered**: A profile `/me` projection and exposing internal entities were rejected as PII/persistence coupling.

## Decision: uniform failure

**Rationale**: Invalid consumer secret, wrong application, invalid user credential, revoked/expired session, and ineligible state all produce the same safe unauthorized response. Logs/events contain only identifiers and result category.
