# Quickstart Validation: Password Management

Set an ignored protected development delivery path, then run the existing Docker SDK/API stack.

1. Create an active user, membership, and session. Change password with its bearer credential; expect `204`, revoked old session, failed old-password login, and successful new-password login.
2. Request recovery for known, unknown, and inactive emails; expect identical `202` response. Retrieve the instruction only from the protected local file.
3. Reset with delivered credential; expect `204`, cleared lockout, revoked sessions, failed old password, and successful new password. Reuse fails safely.
4. Exceed recovery/reset configured limits; expect `429`. Inspect logs/events for absence of passwords, credentials, hashes, and stamps.
