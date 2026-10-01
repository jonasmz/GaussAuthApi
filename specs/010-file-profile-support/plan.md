# Implementation Plan: Secure Profile Image Support

**Branch**: `010-file-profile-support` | **Date**: 2026-10-01 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/010-file-profile-support/spec.md`

**Note**: This template is filled in by the `$speckit-plan` command; its definition describes the execution workflow.

## Summary

Provide one global profile avatar per user with authenticated self-service mutation and public opaque-reference retrieval. Validate JPEG/PNG/WebP bytes, dimensions and decodeability; remove embedded metadata by safe decode/re-encode; store only an opaque reference in UserProfile and use local storage with compensation for database/filesystem failures.

## Technical Context

<!--
  ACTION REQUIRED: Replace the content in this section with the technical details
  for the project. The structure here is presented in advisory capacity to guide
  the iteration process.
-->

**Language/Version**: C# / .NET 10

**Primary Dependencies**: ASP.NET Core minimal APIs, EF Core, PostgreSQL 17, ImageSharp (Infrastructure-only image decoding and re-encoding)

**Storage**: PostgreSQL reference metadata plus configured local filesystem root; generated temporary and final avatar files

**Testing**: MSTest integration, migration, security, and filesystem-adapter tests in the Docker SDK container

**Target Platform**: Containerized Linux Web API

**Project Type**: Web service

**Performance Goals**: Bounded 5 MB uploads; 4096×4096 maximum dimensions; public versioned avatar reads use HTTP cache headers

**Constraints**: JPEG/PNG/WebP only; re-encode stripped metadata before final storage; no filesystem/HTTP dependency in Domain; no cloud storage, resizing, workers, or generic file subsystem

**Scale/Scope**: One avatar reference per global user; public reads only through opaque current/previous references

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Gate | Result | Evidence |
|---|---|---|
| Bounded Auth service | PASS | Global avatars are part of global user profiles; no business assets added. |
| Hexagonal dependencies | PASS | Domain stores only opaque reference; Application uses ports; Infrastructure owns storage and image library. |
| File security | PASS | Allowlist, content/decode validation, bounded dimensions/bytes, generated names, controlled root, and compensation are designed. |
| Application isolation | PASS | Avatar is global identity data; mutations use current authenticated UserId and no caller-selected ID. |
| Simplicity | PASS | Reuses UserProfile reference and local storage; no generic StoredFile, cloud service, queue, or distributed transaction. |

## Project Structure

### Documentation (this feature)

```text
specs/010-file-profile-support/
├── plan.md              # This file ($speckit-plan command output)
├── research.md          # Phase 0 output ($speckit-plan command)
├── data-model.md        # Phase 1 output ($speckit-plan command)
├── quickstart.md        # Phase 1 output ($speckit-plan command)
├── contracts/           # Phase 1 output ($speckit-plan command)
└── tasks.md             # Phase 2 output ($speckit-tasks command - NOT created by $speckit-plan)
```

### Source Code (repository root)
<!--
  ACTION REQUIRED: Replace the placeholder tree below with the concrete layout
  for this feature. Delete unused options and expand the chosen structure with
  real paths (e.g., apps/admin, packages/something). The delivered plan must
  not include Option labels.
-->

```text
src/
├── GaussAuth.Domain/Users/
├── GaussAuth.Application/Profiles/Avatars/
│   └── Ports/
├── GaussAuth.Infrastructure/ProfileImages/
├── GaussAuth.Infrastructure/Persistence/
└── GaussAuth.Api/Profiles/

tests/GaussAuth.Foundation.Tests/
└── profileImageTests.test.cs
```

**Structure Decision**: Extend the existing global UserProfile with an opaque avatar reference. Keep multipart transport in API, lifecycle orchestration in the Profiles/Avatars application slice, and ImageSharp/local storage in Infrastructure.

## Post-Design Constitution Check

All gates remain PASS. ImageSharp is a focused Infrastructure dependency justified by the explicit metadata stripping and safe decode/re-encode requirement; its license must be verified before implementation. No prohibited infrastructure is introduced.
