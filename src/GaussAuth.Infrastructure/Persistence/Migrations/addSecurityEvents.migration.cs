using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GaussAuth.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSecurityEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SecurityEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Outcome = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ApplicationId = table.Column<Guid>(type: "uuid", nullable: true),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: true),
                    ConsumerApplicationId = table.Column<Guid>(type: "uuid", nullable: true),
                    CorrelationId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    SubjectType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    SubjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    Reason = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Metadata = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SecurityEvents", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SecurityEvents_ApplicationId_OccurredAtUtc_Id",
                table: "SecurityEvents",
                columns: new[] { "ApplicationId", "OccurredAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_SecurityEvents_EventType_OccurredAtUtc_Id",
                table: "SecurityEvents",
                columns: new[] { "EventType", "OccurredAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_SecurityEvents_OccurredAtUtc_Id",
                table: "SecurityEvents",
                columns: new[] { "OccurredAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_SecurityEvents_SessionId_OccurredAtUtc_Id",
                table: "SecurityEvents",
                columns: new[] { "SessionId", "OccurredAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_SecurityEvents_UserId_OccurredAtUtc_Id",
                table: "SecurityEvents",
                columns: new[] { "UserId", "OccurredAtUtc", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SecurityEvents");
        }
    }
}
