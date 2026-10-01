# Research: Secure Profile Images

## Decision: decode, sanitize, and re-encode with SkiaSharp

**Rationale**: SkiaSharp (MIT, no license key or fee) decodes and encodes JPEG, PNG, and WebP on Linux. `SKCodec` identifies the real format and dimensions from content before any pixel decode, and decoding the first frame into a raw bitmap then re-encoding drops every metadata block (EXIF, XMP, ICC, text chunks). It stays entirely in Infrastructure; the Linux native asset (`SkiaSharp.NativeAssets.Linux.NoDependencies`) is bundled, so the SDK/runtime images need no extra packages.

**Alternatives considered**: SixLabors.ImageSharp was adopted first and replaced because its Split License requires a paid commercial license (and a license key at build) for non-qualifying commercial use. `System.Drawing.Common` is unsupported for this Linux deployment; Magick.NET (Apache-2.0) works but is a heavier native dependency.

**License review (T001)**: SkiaSharp 4.153.1 and its Linux native asset are MIT-licensed (Skia itself is BSD-3-Clause); no license key, fee, or warning applies. The dependency is referenced only by `GaussAuth.Infrastructure`.

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
