using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudioCore.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class StudioSettingsAndInvitations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "StudioUser",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Location",
                table: "Contract",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "StudioInvitation",
                columns: table => new
                {
                    TenantId = table.Column<string>(nullable: false, defaultValue: "system-master"),
                    Id = table.Column<string>(nullable: false),
                    Email = table.Column<string>(nullable: false),
                    Role = table.Column<string>(nullable: false),
                    TokenHash = table.Column<string>(nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(nullable: false),
                    AcceptedAt = table.Column<DateTimeOffset>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudioInvitation", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_StudioInvitation_Studios_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Studios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StudioInvitation");

            migrationBuilder.DropColumn(
                name: "Email",
                table: "StudioUser");

            migrationBuilder.DropColumn(
                name: "Location",
                table: "Contract");
        }
    }
}
