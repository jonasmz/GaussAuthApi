# Research: 001-foundation

All decisions below apply only to the foundation feature. They do not select
token format, password policy, production deployment, or other open constitutional
choices.

## Development containers

**Decision**: Use repository-controlled `compose.dev.yml` with two services:
`postgres` on PostgreSQL 17 and `sdk` on the official .NET 10 SDK image.
The existing Codex development container invokes Compose through its mounted host
Docker socket; it is not rebuilt. The SDK service mounts the repository at the
same path visible to the host daemon and communicates with PostgreSQL on the
Compose network. It is used for build, migration, tests, and API execution.

**Rationale**: The existing `speckit-devbox` has the Docker socket and the
current shell and devbox do not have `dotnet`. Separate SDK and database
containers satisfy the constitution without installing software in the
existing container. Compose is available in this environment and makes the
two-service lifecycle reproducible. A named PostgreSQL volume keeps ordinary
development data; explicit volume removal permits clean recreation. An ignored
development-only environment file supplies database credentials; committed
examples contain placeholders only. Avoid printing resolved Compose config
because it can contain interpolated credentials. Compose health checking can
gate database-dependent commands, but startup remains an explicit action.

**Alternatives considered**: Installing the SDK or PostgreSQL into
`speckit-devbox` would alter the existing development environment and violate
the container separation. Ad-hoc `docker run` commands would duplicate
network and volume setup. Kubernetes and a production image are out of scope.

**Sources**: [Docker Compose services](https://docs.docker.com/reference/compose-file/services/),
[health-gated startup](https://docs.docker.com/compose/how-tos/startup-order),
[Compose secrets guidance](https://docs.docker.com/compose/how-tos/use-secrets/),
[official .NET SDK image](https://github.com/dotnet/dotnet-docker/blob/main/README.sdk.md).

## Identity and EF Core persistence

**Pinned foundation dependencies**: ASP.NET Core Identity EF stores, EF Core
runtime, and EF Core Design 10.0.12; Npgsql's EF Core provider 10.0.3; local `dotnet-ef`
10.0.12. The focused test project uses Microsoft.NET.Test.Sdk 18.10.0,
MSTest framework and adapter 4.4.1, and Microsoft.AspNetCore.Mvc.Testing
10.0.12. Package restore and build in the .NET 10 SDK container verify this
combination.

**Decision**: Put a roleless Identity user context in Infrastructure using
`IdentityUser<Guid>` and `IdentityUserContext<IdentityUser<Guid>, Guid>`.
Register Identity core and EF user stores, without role services, sign-in
workflow, cookies, bearer scheme, token provider, or API endpoints. Keep
Identity schema configuration identical at runtime and migration design time.
Set the Identity store schema to Version2 so this foundation does not adopt
the Version3 passkey table; confirm this in the generated migration.
Use the provider `Npgsql.EntityFrameworkCore.PostgreSQL` from the EF 10
generation. Inspect the generated initial migration and snapshot, including
schema-version effects, to ensure only foundation user-side storage and the
EF migration history are created; do not create role, application, permission,
session, or passkey tables. Enforce unique normalized email in the persistence
model because email uniqueness is constitutional, while leaving user creation
and normalization workflow to a later feature.

**Rationale**: Roleless Identity storage proves credential infrastructure
without adopting Identity's global role tables, which do not express the
project's application-scoped role model. Guid keys are stable and usable as
cross-service references; this choice applies to persistence identity only
and does not introduce a domain User type. The Npgsql provider is necessary
for EF Core with PostgreSQL. EF migration history makes repeat application
detectable. The initial schema must be inspected because .NET 10 Identity
supports additional schema versions, including passkey-related storage;
passkeys remain undecided by the constitution.

**Alternatives considered**: A full `IdentityDbContext` with built-in roles
would create a premature global-role schema. A custom user store would
reimplement Identity for no current need. An in-memory provider or another
database would fail the PostgreSQL requirement. A separate migrations
project would add a fifth production project without a current need.

**Sources**: [Identity model customization](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/customize-identity-model?view=aspnetcore-10.0),
[roleless IdentityUserContext](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity-primary-key-configuration),
[Identity schema versions](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.identity.identityschemaversions?view=aspnetcore-10.0),
[AddIdentityCore role behavior](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.dependencyinjection.identityservicecollectionextensions.addidentitycore),
[Npgsql EF provider](https://www.npgsql.org/efcore/),
[Npgsql EF 10 release](https://www.npgsql.org/efcore/release-notes/10.0.html),
[EF migrations](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations).

## Migration tooling and consistency

**Decision**: Track a local `dotnet-ef` tool manifest with the solution.
Put the context, initial migration, and snapshot in Infrastructure; use API
as the EF tooling startup project. The SDK container runs `dotnet tool restore`
and `dotnet ef database update --project src/GaussAuth.Infrastructure
--startup-project src/GaussAuth.Api`. Review and, if necessary, rename
generated migration files to the constitutional `<name>.<type>.cs` format
without changing their generated type identities. Verify that runtime
Identity options and EF design-time options produce the same model.

**Rationale**: EF tooling explicitly supports separate target and startup
projects. A local tool manifest pins tooling for reproducibility. Applying
the migration through the SDK container proves the intended path, and
reapplying it verifies migration history behavior.

**Alternatives considered**: Automatically migrating during API startup
would make startup mutate schema and obscure failure ownership. A global
`dotnet-ef` install would make developer environments diverge. Handwritten
DDL would bypass EF migration governance.

**Sources**: [EF Core CLI and target/startup projects](https://learn.microsoft.com/en-us/ef/core/cli/dotnet),
[EF design-time context creation](https://learn.microsoft.com/en-us/ef/core/miscellaneous/cli/dbcontext-creation),
[Identity migration model consistency](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/customize-identity-model?view=aspnetcore-10.0).

## API composition and safe operations

**Decision**: Use a minimal ASP.NET Core API with one GET liveness route
returning 204 and no response body. The route confirms that the API process
responds; database connectivity is demonstrated separately by migration and
integration validation. Use built-in Problem Details and exception-handling
middleware for a generic production 500 response, and standard ILogger
structured logging. The API composition root calls grouped registration
methods for Application, Infrastructure, and API services. Validate required
database connection configuration on startup without logging its value.

**Rationale**: A liveness route is the smallest contract satisfying the
operational requirement and does not expose database details. Built-in
middleware gives a consistent error shape without a custom framework. The
separate database check avoids coupling liveness to transient database
availability.

**Alternatives considered**: A combined readiness endpoint would add a
second operational contract and reveal infrastructure state without a
requirement. A third-party logging or error package is unnecessary. A test
exception endpoint in production would expand the public attack surface.

**Sources**: [ASP.NET Core error handling](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/error-handling?view=aspnetcore-10.0),
[API Problem Details](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/error-handling-api?view=aspnetcore-10.0),
[ASP.NET Core health checks](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/health-checks?view=aspnetcore-10.0).

## Essential verification

**Decision**: Use one focused test project with Microsoft test tooling,
MSTest, and `Microsoft.AspNetCore.Mvc.Testing` to verify project-reference
direction, API composition/liveness, and safe production errors. Exercise
the initial migration against the dedicated PostgreSQL 17 development
service as an integration check. Static checks also enforce one top-level
type per file and the filename convention; generated files are included.
No coverage percentage is required.

**Rationale**: These tests protect the costly foundation boundaries and
startup behavior. Microsoft testing packages are justified by the required
automated checks; a separate architecture-testing framework is not.

**Alternatives considered**: Testing only method wrappers would mirror
implementation without protecting architecture. An additional test
framework or in-memory database would add dependencies or miss real
persistence behavior.
