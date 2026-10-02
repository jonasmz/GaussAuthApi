# First global administrator

1. Run `dotnet GaussAuth.Api.dll migrate`.
2. Supply `Bootstrap__AdministratorEmail` plus `Bootstrap__AdministratorPassword`, or preferably `Bootstrap__AdministratorPasswordFile` mounted as a secret, then run `dotnet GaussAuth.Api.dll bootstrap-admin`.
3. Record the printed UserId.
4. Set `Administration__GlobalAdministratorUserIds__0` to that id and restart/deploy the API.
5. Log in and verify a global operation such as listing Applications.
6. Remove the bootstrap password from the environment/secret store and rotate it normally if it was exposed.

An empty administrator list means **no global authority**. The command grants no authority itself and refuses once any user exists, so it is intentionally not idempotent and cannot be reused as a backdoor.
