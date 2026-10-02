# Contract: First Administrator Bootstrap Command

A one-off operator command shipped in the production image, used once on a new system to create the first user, who is then made a global administrator through configuration. It corrects a release-blocking gap: user creation requires a global administrator, and the global list only holds ids of existing users, so an empty system could not otherwise be operated.

```text
dotnet GaussAuth.Api.dll bootstrap-admin
```

(container: `docker run … <image> bootstrap-admin`).

| Aspect | Contract |
|--------|----------|
| Host | Minimal separate host (database context, Identity, existing create-user use case). Needs only `ConnectionStrings__AuthenticationDatabase` plus the bootstrap inputs. Not part of the web host; **no HTTP surface**. |
| Inputs | `Bootstrap__AdministratorEmail`; `Bootstrap__AdministratorPassword` **or** `Bootstrap__AdministratorPasswordFile` (mounted secret preferred); optional `Bootstrap__AdministratorFirstName` / `…LastName` (defaults `Auth` / `Administrator`). **Never** command-line arguments. |
| Rules applied | The existing create-user rules: email normalization and uniqueness, Identity password policy, profile validation, active user. Nothing is duplicated or bypassed. |
| Precondition | The user store is **empty**. If any user exists the command refuses, exits non-zero, writes nothing. This prevents later use as a backdoor. |
| Output (success) | exit `0`; prints the new `UserId` and the instruction to set `Administration__GlobalAdministratorUserIds__0=<UserId>` and restart. |
| Output (failure) | exit `≠ 0`; value-free reason category (`users-exist`, `invalid-email`, `password-policy`, `missing-input`, `database`, `configuration`); password policy errors name rules, never echo the password. |
| Secrecy | The password is never printed, logged, audited, or included in errors. |
| Authority | **None granted by the command.** Global authority exists only when the UserId is listed in `Administration:GlobalAdministratorUserIds`. An empty list means no global authority (feature 011). |
| Idempotence | Not idempotent by design: a second run refuses (`users-exist`). |

## Documented procedure (`docs/bootstrap.md`)

1. Run the migration command (database schema exists).
2. Provide the bootstrap email and password (environment variable or mounted file) and run `bootstrap-admin`.
3. Record the printed `UserId`.
4. Set `Administration__GlobalAdministratorUserIds__0` to that id in the API configuration and restart (or deploy) the API.
5. Log in as that user, call a global operation (for example list Applications) and confirm success.
6. Remove the bootstrap password from the environment / secret store; rotate it via normal password management if it was ever exposed.

Until step 4 the system has no global authority and global operations return the existing rejection.
