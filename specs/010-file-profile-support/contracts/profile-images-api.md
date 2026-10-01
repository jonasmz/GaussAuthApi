# Profile Images API Contract

## `PUT /me/profile/avatar`

Authenticated multipart upload with exactly one `file` part. The user identity comes from the valid bearer session; no request user ID is accepted.

| Result | Meaning |
|---|---|
| 200 | Valid image became current; response contains updated safe profile data including opaque avatar reference/URL. |
| 400 | Invalid multipart shape. |
| 401 | Missing or invalid session. |
| 413 | Configured request/file limit exceeded. |
| 415 | Unsupported actual image type. |
| 422 | Allowed signature but invalid, unsafe, or over-dimension image. |
| 429 | Configured profile-image write limit reached. |

The request filename and declared content type are not returned, trusted, or used as storage names.

## `DELETE /me/profile/avatar`

Authenticated self-service removal. Returns `204` whether an avatar was removed or no avatar was current. It is rate limited with profile-image write policy.

## `GET /profile-images/{avatarReference}`

Public retrieval by a strict opaque known reference. Returns trusted `image/jpeg`, `image/png`, or `image/webp`, `Content-Disposition: inline`, `X-Content-Type-Options: nosniff`, and version-safe public cache headers. Unknown, retired, and malformed references all return a safe `404`; no filesystem path or original filename is disclosed.
