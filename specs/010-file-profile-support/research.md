# Research: Secure Profile Images

## Decision: decode, sanitize, and re-encode with ImageSharp

**Rationale**: ImageSharp supports JPEG, PNG, and WebP on Linux, detects/decodes actual content, permits dimension validation, and can clear EXIF/IPTC/XMP metadata before an explicit re-encode. It stays entirely in Infrastructure.

**Alternatives considered**: `System.Drawing.Common` is unsupported for this Linux deployment; SkiaSharp introduces native binaries; Magick.NET is heavier. ImageSharp licensing must be verified before adoption.

**License review (T001)**: SixLabors.ImageSharp 4.1.2 is distributed under the Six Labors Split License: free under Apache-2.0 for open-source use and for qualifying small organizations, with a paid commercial license required otherwise. The project owner approved its use (2026-10-01) and is responsible for confirming the license tier applies to their deployment. The dependency is referenced only by `GaussAuth.Infrastructure`.

## Decision: opaque versioned reference on UserProfile

**Rationale**: Keep the existing avatar reference as a generated UUID plus validated extension. It is enough for one avatar, avoids a speculative generic file entity, never exposes a path or PII, and changes on replacement for simple cache invalidation.

**Alternatives considered**: a generic StoredFile entity, filename-derived paths, and database blobs were rejected as unnecessary or unsafe.

## Decision: controlled public retrieval

**Rationale**: `GET /profile-images/{reference}` validates the opaque reference against a strict format and known storage mapping, infers trusted media type from validated output, uses inline disposition, and returns safe 404 for unknown/malformed values. Public cache is safe because replacement issues a new reference.

**Alternatives considered**: static-file exposure of the storage root and public user-ID paths were rejected for traversal/enumeration risk.

## Decision: compensate across filesystem and profile persistence

**Rationale**: Validate and write generated temporary content first, promote it only within a short profile update transaction, delete the new file on failed persistence, and delete the old file only after a successful commit. Cleanup failures are structured-log observable and leave a recoverable orphan, never a broken profile reference.

**Alternatives considered**: deleting old files before commit, distributed transactions, and a background platform were rejected.

## Decision: profile-row serialization for concurrent changes

**Rationale**: Acquire a short database lock while switching the avatar reference. Image processing occurs before the lock; competing updates then observe the latest reference and delete only their own predecessor.

**Alternatives considered**: distributed locks and uncoordinated last-write-wins were rejected.
