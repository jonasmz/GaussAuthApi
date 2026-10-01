# Tasks: Secure Profile Image Support

**Input**: Design documents from `/specs/010-file-profile-support/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/profile-images-api.md, quickstart.md

**Tests**: Essential upload, storage, isolation, replacement, removal, migration, and safety tests are required by the specification. Run them in the Docker SDK container.

**Organization**: Tasks are intentionally sequential. The constitution requires one contextual implementation path because profile state, local files, database transactions, and security events overlap.

## Phase 1: Setup

**Purpose**: Establish safe project-scoped configuration and dependency review.

- [ ] T001 Review ImageSharp license suitability, add the approved Infrastructure-only package, and document it in `src/GaussAuth.Infrastructure/GaussAuth.Infrastructure.csproj` and `specs/010-file-profile-support/research.md`.
- [ ] T002 Add ignored private profile-image root configuration, project-scoped development volume/bind behavior, and safe examples in `.gitignore`, `.env.example`, and `compose.dev.yml` without versioning storage paths containing user content.

---

## Phase 2: Foundational

**Purpose**: Establish the opaque avatar model, controlled storage, validation, and persistence boundary required by all stories.

- [ ] T003 Extend `src/GaussAuth.Domain/Users/userProfile.entity.cs` with controlled set/clear avatar-reference transitions that accept only an opaque logical reference and never paths, file bytes, or HTTP/filesystem types.
- [ ] T004 Add avatar application results, commands, limits, image asset records, and focused storage/processor/profile-lock ports in `src/GaussAuth.Application/Profiles/Avatars/` and `src/GaussAuth.Application/Profiles/Avatars/Ports/`.
- [ ] T005 Add `ProfileImages` options in `src/GaussAuth.Infrastructure/ProfileImages/` enforcing default maximum input/output size 5 MB and maximum dimensions 4096×4096, both configurable; reject invalid configuration at startup.
- [ ] T006 Implement ImageSharp image inspection/sanitization in `src/GaussAuth.Infrastructure/ProfileImages/` that accepts only JPEG/PNG/WebP based on actual content, decodes safely, enforces dimension/output limits, strips EXIF/IPTC/XMP metadata, and re-encodes only an allowed format.
- [ ] T007 Implement local generated-name temporary/final storage and trusted opaque-reference parsing in `src/GaussAuth.Infrastructure/ProfileImages/`; resolve only beneath configured root and never use original filename, client MIME, PII, or untrusted paths.
- [ ] T008 Add locked profile retrieval/reference persistence support and EF migration/model updates in `src/GaussAuth.Application/Users/Ports/`, `src/GaussAuth.Infrastructure/Persistence/`, and `src/GaussAuth.Infrastructure/Persistence/Migrations/` for serialized avatar updates without a generic StoredFile entity.
- [ ] T009 Register ImageSharp adapter, local storage, profile locking, limits, and safe cleanup logging in `src/GaussAuth.Infrastructure/DependencyInjection/infrastructureServiceCollectionExtensions.extension.cs`.
- [ ] T010 Add foundation tests in `tests/GaussAuth.Foundation.Tests/profileImageTests.test.cs` proving generated opaque references, no traversal/original-name use, accepted JPEG/PNG/WebP signatures, invalid image rejection, metadata stripping, configured byte/dimension limits, and Domain/Application dependency boundaries.

**Checkpoint**: Files can be safely inspected, sanitized, stored, resolved, and compensated without exposing a public route.

---

## Phase 3: User Story 1 - Manage My Profile Image (Priority: P1) 🎯 MVP

**Goal**: An authenticated user creates, replaces, and idempotently removes only their own global avatar.

**Independent Test**: Authenticate a user, upload a valid image, replace it, remove it twice, and verify the profile reference and local files transition correctly without accepting another user ID.

- [ ] T011 [US1] Add failing self-service upload/replacement/removal integration scenarios in `tests/GaussAuth.Foundation.Tests/profileImageTests.test.cs` for authenticated ownership, profile response/avatar URL, idempotent delete, old-file cleanup, and unrelated-file protection.
- [ ] T012 [US1] Implement `ProfileAvatarService` in `src/GaussAuth.Application/Profiles/Avatars/` to validate/sanitize before its short profile transaction, compensate newly created files on promotion/persistence failure, commit the reference before retiring the old file, and emit safe observable cleanup failures.
- [ ] T013 [US1] Add centrally cataloged `profile.avatar.updated`, `profile.avatar.removed`, and meaningful allow-listed rejected-upload events in `src/GaussAuth.Application/Security/` and integrate their safe recording in `src/GaussAuth.Application/Profiles/Avatars/`.
- [ ] T014 [US1] Add `PUT /me/profile/avatar` and `DELETE /me/profile/avatar` multipart mappings in `src/GaussAuth.Api/Profiles/` that derive UserId only from valid bearer session, accept exactly one file part, map safe failures to 400/401/413/415/422, and never echo original filename or declared MIME.
- [ ] T015 [US1] Register configurable `profile-image-write` rate limiting, multipart/request/header limits, and application services in `src/GaussAuth.Api/DependencyInjection/` and `src/GaussAuth.Api/Program.cs`.

**Checkpoint**: A user can safely create, replace, and remove their own avatar without profile/file divergence in normal and compensated failure paths.

---

## Phase 4: User Story 2 - Safely Retrieve a Profile Image (Priority: P1)

**Goal**: Any reader can retrieve only a known current image through its opaque public reference.

**Independent Test**: Upload an avatar, retrieve the returned opaque URL anonymously, and verify trusted media/headers; unknown, retired, malformed, and traversal references produce only safe 404 responses.

- [ ] T016 [US2] Add failing public retrieval contract scenarios in `tests/GaussAuth.Foundation.Tests/profileImageTests.test.cs` for trusted JPEG/PNG/WebP type, inline/no-sniff headers, opaque cache behavior, unknown/retired/malformed references, and no path disclosure.
- [ ] T017 [US2] Implement read-only known-reference lookup/streaming in `src/GaussAuth.Application/Profiles/Avatars/` and `src/GaussAuth.Infrastructure/ProfileImages/` using only parsed opaque references and trusted stored media type.
- [ ] T018 [US2] Add public `GET /profile-images/{avatarReference}` in `src/GaussAuth.Api/Profiles/` with safe 404 normalization, trusted inline content disposition, configured versioned cache policy, and no static-file root exposure.

**Checkpoint**: Public retrieval serves only controlled avatar assets and never permits arbitrary local-file access.

---

## Phase 5: User Story 3 - Reject Unsafe Uploads and Recover Safely (Priority: P1)

**Goal**: Unsafe uploads and filesystem/database partial failures are safe, observable, and recoverable.

**Independent Test**: Force signature/decode/size/dimension failures and storage/persistence/old-cleanup failures; verify safe response, stable profile reference, compensation, and safe logs/events.

- [ ] T019 [US3] Add failure-injection tests in `tests/GaussAuth.Foundation.Tests/profileImageTests.test.cs` for spoofed content/MIME, SVG, oversized body/file/output, undecodable image, concurrent replacements, storage-after-validation failure, persistence-after-write failure, and old-file cleanup failure.
- [ ] T020 [US3] Harden compensation and concurrency handling in `src/GaussAuth.Application/Profiles/Avatars/`, `src/GaussAuth.Infrastructure/ProfileImages/`, and `src/GaussAuth.Infrastructure/Persistence/` so a profile never commits a nonexistent reference and orphan cleanup failures are structured-log observable by UserId/reference/operation/outcome only.
- [ ] T021 [US3] Add architecture/error/log safety checks in `tests/GaussAuth.Foundation.Tests/architectureTests.test.cs` and `tests/GaussAuth.Foundation.Tests/profileImageTests.test.cs` proving Domain/Application do not depend on filesystem/ASP.NET/ImageSharp, and responses/log events omit paths, raw bytes, multipart bodies, credentials, and original filenames.

**Checkpoint**: Invalid uploads and partial failures preserve a valid current profile state with observable, recoverable cleanup behavior.

---

## Phase 6: Polish and Validation

- [ ] T022 Verify avatar migration, nullable reference behavior, profile locking, and existing identity/application constraints in `tests/GaussAuth.Foundation.Tests/migrationTests.test.cs` and `tests/GaussAuth.Foundation.Tests/profileImageTests.test.cs`.
- [ ] T023 Finalize allowed types/limits, metadata stripping, opaque public retrieval, replacement/removal compensation, storage root/volume, retention-free orphan policy, and coordinated backup/restore guidance in `specs/010-file-profile-support/quickstart.md`, `contracts/profile-images-api.md`, and a new `specs/010-file-profile-support/security-review.md`.
- [ ] T024 Run `dotnet test GaussAuth.slnx` inside the SDK Docker container, execute every `quickstart.md` scenario, perform a sensitive path/content hygiene scan, and validate `git diff --check` for `specs/010-file-profile-support/`, `src/`, and `tests/`.

## Dependencies & Execution Order

- Phase 1 precedes Phase 2.
- T003–T010 block all stories.
- US1 depends on controlled processing/storage and establishes the current avatar lifecycle.
- US2 depends on the stored opaque-reference contract from US1.
- US3 validates and hardens the shared lifecycle after US1/US2 behavior exists.
- Polish follows all stories.

## Execution Discipline

Execute tasks in numerical order. No `[P]` tasks are marked because Domain profile updates, local files, cleanup, event writes, and regression tests share the same state and must remain coordinated.

## Implementation Strategy

1. Build validated, metadata-free local storage around the existing opaque profile reference.
2. Deliver authenticated self-service avatar lifecycle as the MVP.
3. Add safe public opaque-reference retrieval.
4. Prove compensating failure and concurrency behavior, document operations, then run the full Docker validation suite.
