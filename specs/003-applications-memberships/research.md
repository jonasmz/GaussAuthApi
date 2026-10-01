# Research: Applications and Memberships

## Application and membership persistence

**Decision**: Add `Applications` and `ApplicationMemberships` to the existing `AuthenticationDbContext`. Store normalized application code in `Code`; use named unique indexes `IX_Applications_Code` and `IX_ApplicationMemberships_UserId_ApplicationId`. Make membership foreign keys reference the existing domain `Users` table and `Applications` table, both with restrictive deletion behavior.

**Rationale**: The global domain User, rather than its ASP.NET Core Identity row, is the business identity membership must reference. Database constraints are the authoritative protection against concurrent duplicate submissions and orphaned memberships. Restrictive relationships preserve history and avoid a future delete operation silently erasing memberships.

**Alternatives considered**: A composite membership key was rejected because a stable membership identifier is required. A foreign key to `AspNetUsers` was rejected because it couples the domain relationship to Infrastructure. Application-only uniqueness checks were rejected because they race.

## Membership eligibility and parent lifecycle

**Decision**: Persist membership state independently. Creating a membership for an inactive application is rejected; for an inactive user and active application, create an inactive historical membership. Activation requires an active user and application. User/application deactivation never cascades a membership state change; eligibility is the conjunction of all three active states.

**Rationale**: This preserves the clarification and prevents a change in one context from mutating relationships in other contexts.

**Alternatives considered**: Cascade-deactivate memberships was rejected by the clarification. Allowing memberships for inactive applications was rejected by the specified conservative creation policy.

## Application services, errors, and concurrency

**Decision**: Use feature-specific `IApplicationRepository` and `IApplicationMembershipRepository` ports with handlers per use case. Reuse `IUserRepository.GetByIdAsync` for membership policy. Adapters translate only relevant named PostgreSQL unique violations into expected duplicate results.

**Rationale**: This mirrors the existing Users slices, retains dependency direction, and gives clear 404/409 responses without exceptions as ordinary control flow.

**Alternatives considered**: A generic repository/result framework and an extra transaction abstraction were rejected as unnecessary abstraction.

## HTTP contract and input validation

**Decision**: Use `/applications` as the context. Membership retrieve, creation, and lifecycle routes are nested under an explicit application id; limited user/application membership list routes return only that subject's relationships. Use 400 for invalid input, 404 for missing resources, and 409 for duplicate or state-conflict operations.

**Rationale**: Nested context prevents accidental cross-application inference, while required lists remain operational views rather than authorization checks.

**Alternatives considered**: User-only mutation routes and a global active-membership reporting endpoint were rejected because they weaken explicit context or exceed scope.

## Bounded collection listings

**Decision**: Every application or membership list is ordered by stable
ascending identifier and uses an opaque continuation cursor representing the
last returned identifier. The default limit is 50 and valid limits are 1–100.

**Rationale**: Stable key ordering prevents duplicate/omitted records within a
continuation sequence and gives the required explicit collection bound without
adding a reporting/search feature.

**Alternatives considered**: Offset paging was rejected because it becomes
unstable as records change; unbounded arrays were rejected by the constitution's
explicit collection-limit requirement.

## Development and verification workflow

**Decision**: Reuse `compose.dev.yml`'s PostgreSQL 17 and .NET 10 SDK services; run migrations, API, and tests from `sdk`. Extend the existing MSTest integration suite and migration schema test.

**Rationale**: This satisfies the constitution and follows the established foundation/user-feature workflow.

**Alternatives considered**: Host SDK/PostgreSQL or an additional test database container were rejected by project development constraints.
