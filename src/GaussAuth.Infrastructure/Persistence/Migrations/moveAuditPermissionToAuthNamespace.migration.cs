using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GaussAuth.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MoveAuditPermissionToAuthNamespace : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Every Application that used the legacy audit permission gets the platform permission, reviewers keep their access
            // through copied role assignments, and the legacy permission is deactivated (history is kept).
            migrationBuilder.Sql(@"
INSERT INTO ""Permissions"" (""Id"", ""ApplicationId"", ""Code"", ""Description"", ""IsActive"", ""CreatedAt"", ""UpdatedAt"")
SELECT gen_random_uuid(), old.""ApplicationId"", 'auth.security.audit.read', 'Read this application''s security audit events.', TRUE, now(), now()
FROM ""Permissions"" old
WHERE old.""Code"" = 'audit.events.read'
ON CONFLICT (""ApplicationId"", ""Code"") DO NOTHING;");

            migrationBuilder.Sql(@"
INSERT INTO ""RolePermissions"" (""Id"", ""ApplicationId"", ""RoleId"", ""PermissionId"", ""IsActive"", ""CreatedAt"", ""UpdatedAt"")
SELECT gen_random_uuid(), rp.""ApplicationId"", rp.""RoleId"", target.""Id"", TRUE, now(), now()
FROM ""RolePermissions"" rp
JOIN ""Permissions"" old ON old.""Id"" = rp.""PermissionId"" AND old.""Code"" = 'audit.events.read'
JOIN ""Permissions"" target ON target.""ApplicationId"" = old.""ApplicationId"" AND target.""Code"" = 'auth.security.audit.read'
WHERE rp.""IsActive""
ON CONFLICT (""RoleId"", ""PermissionId"") DO UPDATE SET ""IsActive"" = TRUE, ""UpdatedAt"" = now()
WHERE NOT ""RolePermissions"".""IsActive"";");

            migrationBuilder.Sql(@"
UPDATE ""Permissions"" SET ""IsActive"" = FALSE, ""UpdatedAt"" = now()
WHERE ""Code"" = 'audit.events.read' AND ""IsActive"";");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Restores the legacy permission; the copied role assignments are kept because they cannot be told apart from later ones.
            migrationBuilder.Sql(@"
UPDATE ""Permissions"" SET ""IsActive"" = TRUE, ""UpdatedAt"" = now() WHERE ""Code"" = 'audit.events.read' AND NOT ""IsActive"";");
        }
    }
}
