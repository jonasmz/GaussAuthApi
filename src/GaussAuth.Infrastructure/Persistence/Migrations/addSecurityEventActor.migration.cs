using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GaussAuth.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSecurityEventActor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ActorUserId",
                table: "SecurityEvents",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SecurityEvents_ActorUserId_OccurredAtUtc_Id",
                table: "SecurityEvents",
                columns: new[] { "ActorUserId", "OccurredAtUtc", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SecurityEvents_ActorUserId_OccurredAtUtc_Id",
                table: "SecurityEvents");

            migrationBuilder.DropColumn(
                name: "ActorUserId",
                table: "SecurityEvents");
        }
    }
}
