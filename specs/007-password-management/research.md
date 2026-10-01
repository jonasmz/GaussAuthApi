# Research: Password Management

## Decisions

- **Credential lifecycle**: Identity infrastructure validates current passwords, policy, reset credentials, lockout, and concurrency; Application receives only safe outcomes through focused ports. Custom hashes/tokens are rejected.
- **Session policy**: A per-user Session use case revokes every persisted session after successful change/reset; no second revocation mechanism.
- **Delivery**: A focused delivery port writes instructions to a configured protected local file in Development/Test only. It is ignored, never public, and provider selection remains deferred.
- **Public failures**: Recovery returns identical `202` for every syntactically valid email; reset invalid credential/account failures use one safe `400`; both use configurable IP rate limits.
- **Lockout**: Successful reset clears Identity lockout. Inactive users remain inactive.
