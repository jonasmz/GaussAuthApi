# GaussAuthApi Constitution

## Core Principles

### I. Bounded Authentication Service

The API MUST serve multiple independent business applications and MUST be responsible only
for identity, authentication, authorization, users, credentials, global user profiles,
registered applications, application memberships, roles, permissions, role-permission
assignments, user-role assignments, authenticated sessions, credential/token issuance
and revocation, password management, and security-related audit events. It MUST remain
independent of consuming applications' business domains. It MUST NOT contain business
entities or rules such as reservations, payments, products, orders, inventory, customers,
football fields, invoices, employees, branches, or equivalent concepts. Business APIs
MUST NOT access its database directly; they MUST integrate through explicit contracts
such as credentials/tokens, claims, authorization information, or APIs.

### II. Hexagonal Architecture and Vertical Slices

The project MUST use Hexagonal Architecture / Ports and Adapters, with dependencies
directed toward the application and domain core: API/inbound adapters → Application →
Domain. Infrastructure MUST implement outbound ports defined by inner layers. The
Domain MUST NOT depend on ASP.NET Core, ASP.NET Core Identity, EF Core, PostgreSQL,
HTTP, controllers/endpoints, DTOs, JWT or another concrete token implementation,
filesystem APIs, serialization frameworks, or external infrastructure. ASP.NET Core
Identity MUST be infrastructure; domain users MUST NOT inherit from or depend on
IdentityUser or equivalent framework types. Application functionality MUST be organized
primarily by vertical feature slices, such as Login, Logout, PasswordRecovery,
ChangePassword, Users, Profiles, Applications, Memberships, Roles, Permissions, and
Sessions. Slices MUST contain only use-case elements; hexagonal boundaries MUST prevail.
Unneeded horizontal abstractions MUST NOT be created for architectural symmetry.

### III. Domain Identity and Application Isolation

The initial conceptual domain MUST include User, UserProfile, Application,
ApplicationMembership, Role, Permission, RolePermission, UserRole, Session, and
SecurityEvent. These concepts MUST remain infrastructure-independent. Identity
persistence models MAY differ but MUST NOT replace the domain model. Email MUST be
the login identifier; authentication MUST use email and credentials. Login email MUST
be normalized consistently and uniquely represented under the Identity implementation.
Disabled/inactive users MUST NOT authenticate. Identity and global profile data MUST
be distinct. Global profiles MAY hold reusable name, surname, display name, avatar
reference, and contact information; application-specific profile data MUST remain in
the consuming application.

Applications MUST be explicit, each with a unique stable identifier/code. A user MAY
belong to multiple applications, but access MUST require an active
ApplicationMembership. Roles and permissions MUST belong to an application. User-role
assignments MUST be scoped to the same application as the user's active membership.
Roles and permissions from one application MUST NOT grant access to another. Identical
role names, including Administrator, MAY exist in separate applications without shared
privileges. Authorization MUST follow least privilege.

### IV. Roles, Permissions, and Sessions

Authorization MUST support roles as groups of granular permissions and permissions as
explicit capabilities, for example reservations.read, reservations.create,
payments.register, or users.manage. This API MUST manage identities, role assignments,
and permission definitions. Business APIs MUST apply authorization to their own
business operations. An authenticated session MUST identify user, application,
creation time, expiration time, and revocation state. Expired or revoked sessions
MUST NOT grant valid access. Token format and revocation strategy MUST be resolved
explicitly in a relevant feature specification or technical plan.

### V. Fixed Technology and Persistence

The backend MUST use .NET 10, C#, ASP.NET Core Web API, ASP.NET Core Identity,
Entity Framework Core, and PostgreSQL 17. Alternative backend platforms, identity
frameworks, ORMs, or databases require an explicit constitutional amendment.
Schema changes MUST use version-controlled, reviewable, reproducible, traceable
EF Core migrations. Persistence implementations MUST reside in Infrastructure;
domain entities MUST NOT use EF Core-specific APIs. Business applications MUST NOT
use foreign keys into the authentication database. Cross-service identity references
SHOULD use stable identifiers instead of physical database relationships.

### VI. Security by Design

Security MUST be considered in specifications, plans, implementations, and reviews.
All external input MUST be treated as untrusted. Input boundaries MUST apply suitable
validation, format verification, length checks, range checks, normalization, and
request limits. Sanitization MUST be contextual and MUST NOT substitute for validation
or output encoding. Database access MUST be parameterized. Secrets MUST NOT be
hard-coded or committed. Passwords, access credentials, secrets, credential material,
and complete authentication tokens MUST NOT be logged. Production errors MUST NOT
expose stack traces, connection strings, SQL statements, secrets, internal filesystem
paths, or unnecessary implementation details.

### VII. Simplicity and Dependency Governance

Code MUST apply SOLID, KISS, YAGNI, and pragmatic engineering. SOLID MUST NOT justify
unneeded abstraction. The simplest solution preserving domain rules, security,
clarity, and maintainability SHOULD be chosen. Speculative features or infrastructure
MUST NOT be added. Message brokers, distributed caches, event sourcing, complex CQRS
frameworks, cloud storage, full OAuth/OIDC server infrastructure, additional mediator,
mapping or validation frameworks, and custom result frameworks MUST NOT be introduced
without a concrete requirement. Native .NET capabilities SHOULD be preferred when
adequate. Before adding third-party libraries, frameworks, infrastructure, or services,
the plan MUST establish necessity and evaluate maintenance burden, security, ecosystem
support, compatibility, architectural impact, and native .NET alternatives. Convenience
alone MUST NOT justify a dependency.

### VIII. Proportionate Testing and Error Contracts

Automated tests MUST cover essential high-value domain, business, security, and data
integrity behavior where applicable: domain invariants; login success/failure;
inactive users/applications; invalid memberships; role assignments; permission
resolution; cross-application isolation; session expiration/revocation; password
recovery/reset security; critical rate limiting; persistence integrity; and upload
security. Testing effort MUST be proportional to risk. Trivial tests solely for
coverage metrics MUST NOT be written; no global coverage percentage is imposed.
API errors MUST use consistent contracts. Expected domain/application failures
MUST map to appropriate API responses. Exceptions MUST NOT serve as ordinary
control flow when an expected-result model is clearer. A custom result framework
MUST NOT be added without a concrete need.

### IX. Specification-Driven Development

Development MUST follow GitHub Spec-Kit: constitution → specify → clarify → plan →
tasks → analyze → implement. This constitution governs all later artifacts. Feature
specifications MUST state required behavior and domain rules; they MUST NOT
unnecessarily prescribe technical details already governed here. Plans MUST explicitly
verify constitutional compliance. Tasks MUST derive from the approved specification
and plan. Implementation MUST follow the constitution, specification, plan, and
tasks. Agents MUST NOT silently weaken requirements to simplify implementation.

## Security and Operational Constraints

- Abuse-prone public or anonymous endpoints MUST use rate limiting when implemented,
  including login, password recovery/reset, email confirmation/resend, anonymous
  credential operations, and computationally expensive public endpoints. Limits MUST
  address brute force, credential stuffing, automated abuse, and application-level
  denial of service. ASP.NET Core Identity lockout SHOULD be used where appropriate.
  Exact thresholds and durations MUST be configurable and defined by the feature
  specification or plan.
- Endpoints MUST define reasonable use-case limits for request body, string length,
  collection size, pagination, metadata, file size, and file count where applicable.
  They MUST NOT rely solely on framework defaults where explicit limits affect security.
- If uploads are required, files MUST initially use local filesystem storage through
  a port/adapter boundary. Application and Domain MUST NOT depend directly on
  physical paths or filesystem implementation. Cloud storage requires an explicit
  new requirement. Uploaded files MUST be placed in a server-controlled location
  where they cannot execute as application content.
- Uploads MUST define a file-type allow-list, maximum size, and maximum count where
  applicable; verify signatures/magic bytes where applicable; and MUST NOT trust only
  extension or client Content-Type. Implementations MUST sanitize and normalize
  original names, prevent path traversal, generate server-controlled storage names,
  and prevent unsafe serving of executable user content. Original names MAY be
  retained only as metadata when needed. File features MUST define creation,
  database failure after creation, replacement, deletion, rollback, and cleanup
  of unreferenced files. Metadata and physical storage MUST NOT silently diverge.
- Logging MUST be structured and use standard .NET abstractions unless a requirement
  justifies an alternative. Security events MUST be auditable where appropriate:
  login success/failure, lockout, password change/recovery/reset, session revocation,
  role assignment, and permission modification. Audit records MUST contain only
  information necessary for traceability.
- Environment-specific configuration MUST remain external to source. Secrets MUST
  NOT be stored in source, committed configuration, repository files, or container
  images. Development and deployment MUST use suitable environment secret mechanisms.

## Development Workflow and Agent Rules

- Exactly one top-level class, interface, record, enum, struct, or delegate MUST appear
  in each C# source file. Filenames MUST identify the primary type and category using
  the convention `<name>.<type>.cs`, for example user.entity.cs,
  userStatus.enum.cs, loginRequest.dto.cs, identityService.interface.cs,
  login.command.cs, login.handler.cs, getProfile.query.cs, or login.validator.cs.
  The suffix vocabulary MUST remain consistent.
- Except where overridden here, current Microsoft/.NET conventions MUST govern C#
  naming, async programming, CancellationToken, nullable references, exceptions,
  dependency injection, configuration, structured logging, resource disposal, and
  HTTP API implementation. Project-specific conventions MUST NOT replace adequate
  standard conventions.
- The built-in ASP.NET Core DI container MUST be used unless an explicit requirement
  justifies replacement. Program.cs MUST remain a readable composition root;
  registrations MUST be grouped by clear layer/module methods such as
  AddApplication(), AddInfrastructure(...), AddIdentityModule(), or AddApiServices(),
  without needless indirection.
- Agents MUST inspect the current project state before reimplementing existing work.
  They MUST minimize unnecessary token use and MUST NOT duplicate analysis without
  reason, run competing implementations, launch speculative agents, or duplicate
  code-generation work.
- Agents MUST NOT launch background forks or implicitly spawn subagents or parallel
  agents. Parallel agent execution MAY occur only when explicitly requested or
  explicitly justified by isolated responsibilities, sufficient context for each
  agent, no uncontrolled overlap, and concrete benefit outweighing token cost.
  Delegated agents MUST receive the relevant constitution, specification, plan,
  tasks, domain rules, architecture, and current implementation state. One
  well-contextualized sequential agent SHOULD be preferred. These rules supersede
  generic Spec-Kit advice that independent tasks may run in parallel. Task markers
  and plans MUST NOT encourage parallelism merely because tasks are independent.
- The following MUST remain undecided until explicitly resolved by a feature
  specification, clarification, or plan: concrete access-token format; JWT versus
  another mechanism; refresh tokens; session duration; token rotation; immediate
  revocation strategy; email provider; email-confirmation policy; MFA; OAuth 2.0;
  OpenID Connect; social login; passkeys; exact password policy; exact lockout
  duration; Docker/containerization; deployment platform; reverse proxy; advanced
  observability; and distributed caching. Agents MUST NOT silently choose them.

## Governance

This constitution is the highest-level technical governance document. Every
specification, plan, task list, analysis, and implementation MUST be checked against it.
Conflicts MUST be identified explicitly and resolved by redesign for compliance or
explicit amendment; silent workarounds are prohibited. Amendments MUST be intentional
and documented, stating the changed principle and why. They SHOULD identify affected
artifacts/code and required migration or remediation. Feature-only requirements MUST
NOT become permanent constitutional rules without a project-wide reason. Ambiguity
MUST be clarified before making a permanent architectural decision. Amendments
SHOULD be infrequent.

Versioning MUST follow semantic versioning: MAJOR for incompatible principle removal
or redefinition, MINOR for new principles/sections or materially expanded guidance,
and PATCH for non-semantic clarification. Ratification date MUST remain the original
adoption date; Last Amended MUST record the date of the latest change. Compliance
reviews MUST verify applicable rules and document required amendments before
conflicting work proceeds.

**Version**: 1.0.0 | **Ratified**: 2026-09-30 | **Last Amended**: 2026-09-30
