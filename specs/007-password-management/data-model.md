# Data Model: Password Management

No Domain entity or database table is added. Identity infrastructure owns password hashes, policy, reset credentials, security stamp, lockout, and concurrency. A protected local recovery instruction is delivery output, never application persistence. Existing `Sessions` rows for the affected `UserId` are revoked after successful password change/reset. Events carry only type and identifiers.
