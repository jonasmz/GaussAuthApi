# Feature Specification: Secure Profile Image Support

**Feature Branch**: `010-file-profile-support`

**Created**: 2026-10-01

**Status**: Draft

**Input**: User profile images can be safely uploaded, retrieved, replaced, and removed without becoming a generic file-management feature.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Manage My Profile Image (Priority: P1)

An authenticated user uploads a profile image for their own global identity, replaces it later, or removes it when it is no longer wanted.

**Why this priority**: A safe self-service avatar is the central outcome of the feature.

**Independent Test**: A user uploads an allowed valid image, receives a profile response with a logical avatar reference, replaces it, retrieves the current image, and removes it; no unrelated profile or file changes.

**Acceptance Scenarios**:

1. **Given** an authenticated user without an image, **When** they upload a valid allowed image, **Then** their global profile references the new logical avatar and it can be retrieved through the permitted access route.
2. **Given** an authenticated user with an image, **When** they upload another valid image, **Then** the new image becomes current and the prior file is safely retired.
3. **Given** an authenticated user with an image, **When** they remove it, **Then** the avatar reference is cleared and only its corresponding stored file is removed.
4. **Given** a caller attempts to choose another user's identity, **When** they upload or remove an image, **Then** the request cannot change that other user's avatar.

---

### User Story 2 - Safely Retrieve a Profile Image (Priority: P1)

An authorized reader retrieves a current profile image without gaining arbitrary filesystem access or receiving untrusted file metadata.

**Why this priority**: An uploaded image is useful only when it can be served safely.

**Independent Test**: Retrieve a known current avatar and verify its trusted media type; request an unknown or traversal-style reference and verify that no server path or unrelated content is disclosed.

**Acceptance Scenarios**:

1. **Given** a current avatar, **When** a caller with the selected read access requests it, **Then** the service returns only that image with a trusted content type and safe inline-serving behavior.
2. **Given** a missing, removed, or malformed reference, **When** it is requested, **Then** the service returns a safe not-found or authorization response without filesystem details.

---

### User Story 3 - Reject Unsafe Uploads and Recover Safely (Priority: P1)

The service rejects unsupported, malformed, spoofed, oversized, or unsafe image uploads and keeps profile metadata and local storage recoverably consistent when failures occur.

**Why this priority**: File handling is security-sensitive and must not leave hidden data loss or orphan files in normal operation.

**Independent Test**: Submit an invalid signature, a spoofed name/type, an oversized payload, and a simulated persistence/cleanup failure; verify safe rejection, observability, and that the profile never points at a nonexistent image.

**Acceptance Scenarios**:

1. **Given** a file whose claimed type or name disagrees with its actual content, **When** it is uploaded, **Then** it is rejected before becoming an avatar.
2. **Given** storage succeeds but the profile update fails, **When** the operation ends, **Then** the newly stored file is cleaned up where practical and the existing avatar remains current.
3. **Given** replacement succeeds but old-file cleanup fails, **When** the operation ends, **Then** the new avatar remains current and the cleanup failure is observable without exposing storage internals.

### Edge Cases

- An upload has an allowed signature but cannot be decoded as a valid image.
- A compressed image stays within the byte limit but exceeds configured dimension or pixel limits.
- Two changes for the same profile arrive concurrently; the final reference must not point to a deleted file.
- Removal is requested when no avatar exists; its idempotency behavior is explicit.
- A legacy avatar reference names a file no longer present; retrieval remains safe and replacement/removal repairs the reference predictably.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST support upload, replacement, retrieval, and removal of exactly one global profile image per user; it MUST NOT introduce general documents, attachments, galleries, sharing, or application-specific assets.
- **FR-002**: Self-service upload, replacement, and removal MUST use the trusted authenticated user identity and MUST NOT accept a caller-selected target user identity.
- **FR-003**: The system MUST allow only [NEEDS CLARIFICATION: which raster formats and default maximum upload size/dimensions are accepted?] and MUST reject SVG and all non-allowlisted content.
- **FR-004**: The system MUST enforce configurable request-body, file-byte, and image-dimension/pixel limits before committing an avatar.
- **FR-005**: The system MUST validate actual file signatures and image decodability; filename extension, multipart declaration, and client MIME type alone MUST never authorize a file type.
- **FR-006**: The system MUST create a server-generated opaque logical reference and storage name. User-provided filenames, names, email, display name, MIME declarations, and paths MUST NOT determine physical storage paths or response headers.
- **FR-007**: The global profile MUST retain only a logical avatar reference and necessary trusted metadata, never file bytes or environment-specific filesystem paths.
- **FR-008**: Stored image bytes MUST remain in a configurable local, server-controlled, non-executable storage root. Domain objects MUST remain independent of filesystem, HTTP, streams, multipart types, and image-processing concerns.
- **FR-009**: Image retrieval MUST use controlled known-avatar resolution, trusted media type, and safe response headers; it MUST prevent traversal and arbitrary local-file access.
- **FR-010**: Profile-image retrieval is [NEEDS CLARIFICATION: publicly readable by opaque reference, authenticated for any valid user, or restricted to the owner?].
- **FR-011**: Replacement MUST validate and persist the new image before it becomes current, clear/retire the prior image only when safe, and prevent normal successful operations from leaving orphan files.
- **FR-012**: Removal MUST clear the avatar reference and remove only the corresponding file. Its no-current-avatar behavior MUST be idempotent or explicitly reported consistently.
- **FR-013**: The system MUST use compensating cleanup for filesystem/database partial failures. Failed cleanup MUST be safely observable and MUST NOT expose physical paths to callers.
- **FR-014**: Upload metadata is [NEEDS CLARIFICATION: served unchanged with embedded EXIF metadata preserved, or metadata must be stripped before storage?]. The selected behavior MUST be documented accurately.
- **FR-015**: Meaningful avatar update, removal, and security-relevant rejection events MUST be auditable without image bytes, credentials, raw multipart content, or unnecessary filesystem details.
- **FR-016**: Logs MUST be structured and limited to safe identifiers and operation results. External errors MUST not disclose physical paths, storage-root configuration, raw content, or internal exception types.
- **FR-017**: Profile-file storage and profile metadata MUST be documented as coordinated backup/restore concerns. Development storage persistence must be deliberate and isolated to this project.
- **FR-018**: The system MUST not introduce cloud storage, CDN, antivirus infrastructure, media processing, thumbnails, resizing, transcoding, generic storage management, or asynchronous workers.

### Key Entities

- **UserProfile avatar reference**: The single opaque global reference to the current user profile image; it has no application ownership and no physical path.
- **Profile image asset**: A locally stored, validated image associated with exactly one global user profile and described only by safe logical metadata.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A valid allowed image can be uploaded, retrieved, replaced, and removed by its owner in all acceptance scenarios without manual storage intervention.
- **SC-002**: 100% of tested invalid signatures, spoofed types, unsupported types, traversal-style names, and oversized uploads are rejected before becoming a profile image.
- **SC-003**: 100% of tested successful replacements remove or retire the prior avatar without changing unrelated assets.
- **SC-004**: 100% of tested partial-failure paths leave the profile either pointing to the prior valid image or to the new valid image, never to a nonexistent file.
- **SC-005**: Retrieval of current images returns only the trusted image type and no server path or user-supplied filename data.
- **SC-006**: Essential upload, replacement, removal, retrieval, isolation, and failure-recovery tests pass.

## Assumptions

- The existing global `UserProfile` avatar reference is the sole data-model extension unless a smaller safe metadata representation is demonstrably insufficient.
- Images are stored unchanged after validation; resizing, transcoding, thumbnails, and generic media workflows are outside the feature.
- Local storage is used through focused application ports; no cloud or remote object storage is selected.
- Existing session authentication, safe errors, rate limiting, and SecurityEvent policies remain authoritative.
- The implementation will use a simple compensating cleanup strategy, not distributed transactions or workers.
