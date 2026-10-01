# Profile Images API Contract

## `PUT /me/profile/avatar`

Authenticated multipart upload with exactly one `file` part. The user identity comes from the valid bearer session; no request user ID is accepted.

| Result | Meaning |
|---|---|
| 200 | Valid image became current; response is the safe profile (`firstName`, `lastName`, `displayName`, `phoneNumber`, `avatarReference`, `avatarUrl`, timestamps). |
| 400 | Not multipart, or not exactly one part named `file` (any extra field, including a user ID, is rejected). |
| 401 | Missing or invalid session. |
| 413 | Configured request/file limit exceeded (default 5 MB; also applies to re-encoded output). |
| 415 | Actual content is not JPEG, PNG, or WebP (SVG, GIF, and others). |
| 422 | Allowed signature but empty, undecodable, or over-dimension image. |
| 429 | Configured profile-image write limit reached. |

The request filename and declared content type are not returned, trusted, or used as storage names.

## `DELETE /me/profile/avatar`

Authenticated self-service removal. Returns `204` whether an avatar was removed or no avatar was current. It is rate limited with profile-image write policy.

## `GET /profile-images/{avatarReference}`

Public retrieval by a strict opaque known reference. Returns trusted `image/jpeg`, `image/png`, or `image/webp`, `Content-Disposition: inline`, `X-Content-Type-Options: nosniff`, and version-safe public cache headers. Unknown, retired, and malformed references all return a safe `404`; no filesystem path or original filename is disclosed.

## Processing and storage guarantees

- Only JPEG, PNG, and WebP are accepted, decided from decoded content. Images are decoded, stripped of EXIF/IPTC/XMP/ICC/CICP metadata, and re-encoded in the same format; the stored bytes are never the uploaded bytes. Animation frames beyond the first are discarded.
- The reference is `<32 lowercase hex>.<jpg|png|webp>`, generated server-side; replacement always issues a new reference, so public caching is safe.
- Replacement and removal commit the profile before retiring the previous file; a failed retirement leaves a logged orphan, never a profile pointing at a missing file.
- Failure responses are generic problem details and contain no paths, filenames, or declared media types.
