# Data Model: Secure Profile Images

## UserProfile avatar reference

`UserProfile.AvatarReference` remains the only persisted avatar field. It contains a server-generated opaque key such as `<uuid>.<validated-extension>` and never an absolute path, original filename, input MIME type, or image bytes.

| Rule | Value |
|---|---|
| Ownership | Exactly one global user profile owns the active reference. |
| Formats | JPEG, PNG, WebP after safe re-encode. |
| Limits | Defaults: 5 MB input/output, 4096×4096 dimensions; deployment-configurable. |
| Lifecycle | null → reference on upload; reference A → B on replacement; reference → null on idempotent removal. |
| Public read | Only by opaque reference; no profile/user identifier route. |

## Local profile image asset

Not a persisted generic entity. Infrastructure maps the logical reference to a path beneath `ProfileImages:StorageRoot` using only generated values. Temporary files live beneath the same controlled root and are promoted/removed by storage operations.

## Consistency transitions

1. Validate bytes, decode, strip metadata, and write generated temporary output.
2. Serialize profile reference update; promote new output and commit reference.
3. On promotion/database failure, rollback and attempt deletion of the new generated artifact.
4. After commit, delete the old reference. If deletion fails, log safe identifiers and leave a recoverable orphan; the profile continues to reference the new image.
5. Removal clears the reference first in the committed profile state, then deletes only the previous generated reference. No reference is an idempotent success.
