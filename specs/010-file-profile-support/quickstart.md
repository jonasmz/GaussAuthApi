# Quickstart Validation: Secure Profile Images

## Configuration

| Setting | Env var | Default | Notes |
|---|---|---|---|
| `ProfileImages:RootPath` | `ProfileImages__RootPath` (compose: `PROFILE_IMAGES_ROOT`) | Development only: `./.profile-images` | **Required** outside Development/Testing; must be an absolute private directory writable by the API user, outside source and web roots. |
| `ProfileImages:MaxBytes` | `ProfileImages__MaxBytes` | `5242880` (5 MB) | Applies to the uploaded file and the re-encoded output. |
| `ProfileImages:MaxDimension` | `ProfileImages__MaxDimension` | `4096` | Maximum width and height in pixels. |
| `RateLimiting:ProfileImageWrite:PermitLimit` / `WindowSeconds` | same, `__` separated | `10` / `60` | Per-IP limit for `PUT`/`DELETE /me/profile/avatar`. |

Invalid or missing configuration fails startup with a generic message. The development container bind-mounts the project directory, so `${PROJECT_DIR}/.profile-images` is project-scoped and git-ignored.

Layout beneath the root: `avatars/<32-hex>.<jpg|png|webp>` (promoted files) and `.staging/<32-hex>.tmp` (in-flight uploads, removed on success or failure).

## Scenarios

Every scenario below is automated in `tests/GaussAuth.Foundation.Tests/profileImageTests.test.cs` (run with the full suite in step 6).

1. Authenticate and upload a valid JPEG, PNG, and WebP to `PUT /me/profile/avatar` (multipart, one `file` part). Confirm `200`, a new opaque `avatarReference`, and `avatarUrl` = `/profile-images/<reference>`.
2. Retrieve `avatarUrl` without authentication. Confirm the trusted `image/*` type, `Content-Disposition: inline`, `X-Content-Type-Options: nosniff`, and `Cache-Control: public, max-age=31536000, immutable`.
3. Upload a spoofed name/MIME, invalid bytes, SVG, oversized input, and oversized dimensions. Confirm `415`/`413`/`422` with no echoed filename or path and no change to the current avatar.
4. Replace an avatar. Confirm the reference changes, the new image is current, and the previous file is retired (its URL now returns `404`). Failure injection covers persistence failure (new file removed, profile unchanged), promotion failure (nothing staged or referenced), and cleanup failure (new avatar current, orphan logged).
5. `DELETE /me/profile/avatar` twice. Confirm `204` both times, a cleared reference, and that other users' and unrelated files are untouched.
6. Run `dotnet test GaussAuth.slnx` inside the SDK container (`docker compose -f compose.dev.yml exec sdk dotnet test GaussAuth.slnx`).

## Backup, restore, and orphans

- The database (`UserProfiles.AvatarReference`) and the `avatars/` directory are one logical dataset: back up and restore them together, taken at the same point in time where possible.
- A restored database may reference a file missing from a restored directory (public retrieval then returns `404`; the user can upload again) and the directory may hold files no profile references.
- Orphans are expected only after a cleanup failure or a crash between write and commit. There is no retention job: log lines `Profile image cleanup left an orphan` (user, reference, operation, outcome only) identify them, and an operator may delete any `avatars/` file not referenced by a profile, plus stale `.staging/` files, while the API is idle.
