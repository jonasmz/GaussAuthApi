# Implementation Plan: Security Audit

**Branch**: `009-security-audit` | **Date**: 2026-10-01 | **Spec**: [spec.md](spec.md)

## Summary

Replace logging-only events with immutable, queryable SecurityEvents; preserve existing security boundaries; harden consumer-secret storage, HTTP behavior, limits, correlation, and operational documentation. Critical administrative changes commit with their audit event where practical; operational event-write failures remain visible without blocking the primary operation.

## Technical Context

**Language/Version**: C# / .NET 10

**Primary Dependencies**: ASP.NET Core minimal APIs and Identity, EF Core, PostgreSQL, built-in rate limiting/logging/tracing, Identity `PasswordHasher`

**Storage**: PostgreSQL 17 authentication database; immutable SecurityEvents table and configuration-injected one-way consumer-secret hashes

**Testing**: MSTest integration, architecture, migration, and regression tests in the Docker SDK container

**Target Platform**: Containerized Linux web API behind production HTTPS termination

**Project Type**: Web service

**Performance Goals**: Indexed newest-first keyset pages; default 50, maximum 100; metadata maximum 2 KiB; no unbounded scans.

**Constraints**: No secrets in events/logs/responses; transactional critical writes where boundaries share a unit of work; no broker, SIEM, cache, or custom cryptography.

**Scale/Scope**: Durable audit stream, protected query surface, focused hardening, consumer-secret hash migration, and security review; no UI, archive, or automatic purge.

## Constitution Check

| Gate | Result | Evidence |
|---|---|---|
| Bounded Auth service | PASS | Auditing, credentials, sessions, and authorization are Auth responsibilities; no business domain is added. |
| Hexagonal dependencies | PASS | Framework-independent SecurityEvent domain and Application ports; Infrastructure owns persistence/hashes/configuration. |
| Application isolation | PASS | Application scope is forced before querying; a separate global reviewer capability is never inferred from application roles. |
| Security by design | PASS | Append-only records, safe metadata, one-way verifier, bounded inputs, safe errors, and focused headers. |
| Simplicity | PASS | Reuses current DbContext, Identity hasher, tracing/logging/rate limiter, and keyset pagination. |

## Project Structure

```text
specs/009-security-audit/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── security-review.md
└── contracts/security-events-api.md

src/
├── GaussAuth.Domain/Security/
├── GaussAuth.Application/Security/
├── GaussAuth.Infrastructure/Security/
├── GaussAuth.Infrastructure/AuthorizationContext/
└── GaussAuth.Api/Security/

tests/GaussAuth.Foundation.Tests/securityAuditTests.test.cs
```

**Structure Decision**: Extend existing Security and AuthorizationContext slices. The domain stays independent from EF, Identity, configuration, logging, and HTTP; shared persistence is used only where critical state/event atomicity requires it.

## Post-Design Constitution Check

All gates remain PASS. No prohibited infrastructure is introduced. Global review is explicit and configuration-backed because no global role model exists; it still requires a valid user session.
