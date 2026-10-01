# Security Review: Secure Profile Images

| Threat | Control | Evidence |
|---|---|---|
| Malicious or spoofed upload (SVG/script, polyglot, wrong extension/MIME) | Type decided from decoded content only; allow-list JPEG/PNG/WebP; client filename and media type never read; output is a fresh re-encode | `Invalid_content_is_rejected_regardless_of_name_or_declared_type`, `Gif_is_rejected_…`, `Unsafe_uploads_are_rejected_…`, architecture test forbids reading `FileName`/declared type |
| Decompression bombs / oversized input | Streaming read bounded by `MaxBytes`; dimensions checked from headers before decode; single frame; request size and form limits; output size re-checked | `Configured_byte_and_dimension_limits_are_enforced`, `Over_dimension_images_and_write_rate_limit_are_enforced` |
| Metadata leakage (GPS, authorship) | EXIF/IPTC/XMP/ICC/CICP removed, encoders skip metadata | `Metadata_is_stripped_on_reencode` |
| Path traversal / arbitrary file read or delete | Names are generated; retrieval accepts only a strictly parsed `<32 hex>.<ext>` reference; staging tokens validated; no static-file exposure of the root | `Malformed_references_are_rejected`, `Storage_uses_generated_names_…`, retrieval traversal cases |
| Cross-user modification | User ID derived only from the bearer session; extra form fields rejected | `Avatar_endpoints_require_a_valid_session_and_ignore_request_user_ids` |
| Enumeration / information disclosure | Opaque random references; unknown, malformed, and retired references are indistinguishable `404`s; errors, logs, and events omit paths, bytes, filenames | `Public_retrieval_…`, `AssertLogsAreSafe`, security-event test |
| Content sniffing / script execution when served | Trusted stored media type, `inline`, `nosniff` | `Public_retrieval_serves_only_current_known_references_with_trusted_headers` |
| Partial failure leaves broken state | Write-then-commit-then-retire ordering with compensation; profile row locked (lock taken in its own statement, stale tracked copies discarded) | `Persistence_failure_…`, `Storage_promotion_failure_…`, `Old_file_cleanup_failure_…`, `Concurrent_replacements_converge_…` |
| Abuse / resource exhaustion | `profile-image-write` rate limit, size limits, bounded buffering | rate-limit test |
| Audit | `profile.avatar.updated`, `.removed`, `.upload.rejected` (allow-listed reason only) | `Avatar_changes_and_rejections_record_allow_listed_security_events` |

## Findings

- **Fixed during implementation:** concurrent replacements initially left orphan files because the profile was read in the same statement as the row lock (and could be a stale tracked copy), so a competing request deleted the wrong predecessor. The lock is now a separate statement followed by a fresh read; covered by the concurrency test.
- **Residual – legacy writable reference:** `PUT /users/{id}/profile` and `POST /users` still accept an arbitrary `AvatarReference` string. It is only ever served if it parses as a valid opaque reference, and `avatarUrl` is only emitted for valid references, but the field should be removed from those requests in a follow-up.
- **Residual – retirement window:** between a replacement's commit and its file deletion the previous URL can still be served briefly.
- **Residual – orphans:** crashes or cleanup failures can leave unreferenced files; see the operator guidance in `quickstart.md`. No retention job exists by design.
- **Residual – native dependency:** SkiaSharp wraps native Skia code; keep `SkiaSharp` and its native asset patched, and keep the input byte/dimension limits (decoding happens only after both pass).
- **Residual – public caching:** images are public by design (global avatar); anyone holding a reference can fetch it until the file is retired.
