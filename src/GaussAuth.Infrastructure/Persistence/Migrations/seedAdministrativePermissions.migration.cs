using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GaussAuth.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SeedAdministrativePermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Fail closed: the "auth." prefix is reserved for platform permissions.
            migrationBuilder.Sql(@"
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM ""Permissions"" WHERE lower(""Code"") LIKE 'auth.%') THEN
        RAISE EXCEPTION 'Existing permissions use the reserved auth. prefix; resolve them before applying this migration.';
    END IF;
END $$;");

            migrationBuilder.Sql(@"
INSERT INTO ""Permissions"" (""Id"", ""ApplicationId"", ""Code"", ""Description"", ""IsActive"", ""CreatedAt"", ""UpdatedAt"")
SELECT gen_random_uuid(), a.""Id"", seed.code, seed.description, TRUE, now(), now()
FROM ""Applications"" a
CROSS JOIN (VALUES
('auth.memberships.read', 'Read application memberships through Auth administration.'),
('auth.memberships.manage', 'Create, activate, and deactivate application memberships through Auth administration.'),
('auth.roles.read', 'Read roles, user-role assignments, and effective authorization through Auth administration.'),
('auth.roles.manage', 'Manage roles and user-role assignments through Auth administration.'),
('auth.permissions.read', 'Read permissions and role-permission assignments through Auth administration.'),
('auth.permissions.manage', 'Manage permissions and role-permission assignments through Auth administration.'),
('auth.sessions.read', 'Read application sessions through Auth administration.'),
('auth.sessions.revoke', 'Revoke application sessions through Auth administration.'),
('auth.security.audit.read', 'Read this application''s security audit events.')
) AS seed(code, description)
ON CONFLICT (""ApplicationId"", ""Code"") DO NOTHING;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DELETE FROM ""Permissions"" p
WHERE p.""Code"" IN ('auth.memberships.read', 'auth.memberships.manage', 'auth.roles.read', 'auth.roles.manage', 'auth.permissions.read', 'auth.permissions.manage', 'auth.sessions.read', 'auth.sessions.revoke', 'auth.security.audit.read')
  AND NOT EXISTS (SELECT 1 FROM ""RolePermissions"" rp WHERE rp.""PermissionId"" = p.""Id"");");
        }
    }
}
