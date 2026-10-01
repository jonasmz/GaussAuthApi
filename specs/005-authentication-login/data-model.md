# Data Model: Application-Scoped Authentication Login

This feature introduces **no new persisted entity and no new EF Core migration**. It reads the existing `User`, `Application`, and `ApplicationMembership` domain entities (from `002-users-profiles` and `003-applications-memberships`) and the existing Identity credential/lockout storage (`AspNetUsers`, already backing `IdentityUser<Guid>`), and produces only transient, in-request values plus a structured log entry.

## Login Request (transient, not persisted)

| Field | Rules |
|---|---|
| ApplicationCode | Required; the stable application code from `003-applications-memberships`; maximum 64 characters. |
| Email | Required; `[EmailAddress]`-valid; maximum 256 characters; normalized using the existing `002-users-profiles` normalization before resolution. |
| Password | Required; maximum 128 characters; never logged, persisted outside Identity, echoed, or included in any exception/telemetry. |

Exists only for the duration of one HTTP request; not stored.

## Login Result (transient, not persisted)

| Field | Rules |
|---|---|
| Outcome | `Success` or `Failure` only; no further detail is part of the public result surface. |
| UserId | Present only when `Outcome = Success`; the existing stable `User.Id`. |
| ApplicationId | Present only when `Outcome = Success`; the existing stable `Application.Id` resolved from the submitted code. |

On `Failure`, no field distinguishes which prerequisite (unknown email, wrong password, inactive user, invalid/inactive application, missing/inactive membership, or lockout) caused the rejection — see `research.md` "Uniform failure response."

## Authentication Security Event (structured log entry, not a new table)

| Field | Rules |
|---|---|
| Type | One of `LoginSucceeded`, `LoginFailed`, `AccountLockedOut`. |
| UserId | Optional; present when the user was resolved before the outcome was determined. |
| ApplicationId | Optional; present when the application was resolved before the outcome was determined. |
| Timestamp | Supplied by the structured-logging framework at write time; not a caller-supplied value. |

Never contains the submitted password, a password hash, or any other credential secret. Implemented behind `ISecurityEventRecorder` (see `research.md`) so a future persisted implementation (`009-security-audit`) can replace the logging-only adapter without changing callers.

## Referential-Integrity Summary

```text
User (existing, 002)              — read-only in this feature
Application (existing, 003)       — read-only in this feature, resolved by Code
ApplicationMembership (existing, 003) — read-only in this feature, resolved by (UserId, ApplicationId)
AspNetUsers (existing Identity row) — read by UserId; password hash and lockout counters
                                        remain entirely inside Identity/Infrastructure
```

No table gains a new column. No new foreign key is introduced. All state transitions already belong to prior features (user activation, application activation, membership activation) or to ASP.NET Core Identity's own lockout bookkeeping; this feature only reads that state.
