using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GaussAuth.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRolesAndPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ApplicationMemberships_UserId_ApplicationId",
                table: "ApplicationMemberships");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_ApplicationMemberships_UserId_ApplicationId",
                table: "ApplicationMemberships",
                columns: new[] { "UserId", "ApplicationId" });

            migrationBuilder.CreateTable(
                name: "Permissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Permissions", x => x.Id);
                    table.UniqueConstraint("AK_Permissions_Id_ApplicationId", x => new { x.Id, x.ApplicationId });
                    table.ForeignKey(
                        name: "FK_Permissions_Applications_ApplicationId",
                        column: x => x.ApplicationId,
                        principalTable: "Applications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    NormalizedName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.Id);
                    table.UniqueConstraint("AK_Roles_Id_ApplicationId", x => new { x.Id, x.ApplicationId });
                    table.ForeignKey(
                        name: "FK_Roles_Applications_ApplicationId",
                        column: x => x.ApplicationId,
                        principalTable: "Applications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RolePermissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    PermissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RolePermissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RolePermissions_Permissions_PermissionId_ApplicationId",
                        columns: x => new { x.PermissionId, x.ApplicationId },
                        principalTable: "Permissions",
                        principalColumns: new[] { "Id", "ApplicationId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RolePermissions_Roles_RoleId_ApplicationId",
                        columns: x => new { x.RoleId, x.ApplicationId },
                        principalTable: "Roles",
                        principalColumns: new[] { "Id", "ApplicationId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UserRoles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRoles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserRoles_ApplicationMemberships_UserId_ApplicationId",
                        columns: x => new { x.UserId, x.ApplicationId },
                        principalTable: "ApplicationMemberships",
                        principalColumns: new[] { "UserId", "ApplicationId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserRoles_Roles_RoleId_ApplicationId",
                        columns: x => new { x.RoleId, x.ApplicationId },
                        principalTable: "Roles",
                        principalColumns: new[] { "Id", "ApplicationId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Permissions_ApplicationId_Code",
                table: "Permissions",
                columns: new[] { "ApplicationId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RolePermissions_PermissionId_ApplicationId",
                table: "RolePermissions",
                columns: new[] { "PermissionId", "ApplicationId" });

            migrationBuilder.CreateIndex(
                name: "IX_RolePermissions_RoleId_ApplicationId",
                table: "RolePermissions",
                columns: new[] { "RoleId", "ApplicationId" });

            migrationBuilder.CreateIndex(
                name: "IX_RolePermissions_RoleId_PermissionId",
                table: "RolePermissions",
                columns: new[] { "RoleId", "PermissionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Roles_ApplicationId_NormalizedName",
                table: "Roles",
                columns: new[] { "ApplicationId", "NormalizedName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserRoles_RoleId_ApplicationId",
                table: "UserRoles",
                columns: new[] { "RoleId", "ApplicationId" });

            migrationBuilder.CreateIndex(
                name: "IX_UserRoles_UserId_ApplicationId",
                table: "UserRoles",
                columns: new[] { "UserId", "ApplicationId" });

            migrationBuilder.CreateIndex(
                name: "IX_UserRoles_UserId_RoleId_ApplicationId",
                table: "UserRoles",
                columns: new[] { "UserId", "RoleId", "ApplicationId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RolePermissions");

            migrationBuilder.DropTable(
                name: "UserRoles");

            migrationBuilder.DropTable(
                name: "Permissions");

            migrationBuilder.DropTable(
                name: "Roles");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_ApplicationMemberships_UserId_ApplicationId",
                table: "ApplicationMemberships");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationMemberships_UserId_ApplicationId",
                table: "ApplicationMemberships",
                columns: new[] { "UserId", "ApplicationId" },
                unique: true);
        }
    }
}
