using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudioCore.Persistence.Migrations;

[DbContext(typeof(StudioDbContext))]
[Migration("20261102140000_GoogleWorkspaceIdentity")]
public sealed class GoogleWorkspaceIdentity : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "GoogleIdentities",
            columns: table => new
            {
                GoogleSubject = table.Column<string>(maxLength: 255, nullable: false),
                Email = table.Column<string>(maxLength: 320, nullable: false),
                DisplayName = table.Column<string>(nullable: false),
                IsActive = table.Column<bool>(nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(nullable: false),
                LastLoginAt = table.Column<DateTimeOffset>(nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_GoogleIdentities", x => x.GoogleSubject));

        migrationBuilder.Sql("""
            INSERT INTO "GoogleIdentities" ("GoogleSubject", "Email", "DisplayName", "IsActive", "CreatedAt", "LastLoginAt")
            SELECT "GoogleSubject", MIN("Email"), MIN("DisplayName"), TRUE, MIN("CreatedAt"), MAX(COALESCE("LastLoginAt", "UpdatedAt"))
            FROM "StudioUser"
            WHERE "GoogleSubject" IS NOT NULL AND "GoogleSubject" <> ''
            GROUP BY "GoogleSubject"
            """);

        migrationBuilder.AddColumn<DateTimeOffset>(name: "CancelledAt", table: "StudioInvitation", nullable: true);
        migrationBuilder.DropIndex(name: "IX_StudioUser_GoogleSubject", table: "StudioUser");
        migrationBuilder.CreateIndex(name: "IX_GoogleIdentities_Email", table: "GoogleIdentities", column: "Email");
        migrationBuilder.CreateIndex(name: "IX_StudioUser_TenantId_GoogleSubject", table: "StudioUser",
            columns: new[] { "TenantId", "GoogleSubject" }, unique: true);
        if (!ActiveProvider.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
            migrationBuilder.AddForeignKey(name: "FK_StudioUser_GoogleIdentities_GoogleSubject", table: "StudioUser",
                column: "GoogleSubject", principalTable: "GoogleIdentities", principalColumn: "GoogleSubject", onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        if (!ActiveProvider.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
            migrationBuilder.DropForeignKey(name: "FK_StudioUser_GoogleIdentities_GoogleSubject", table: "StudioUser");
        migrationBuilder.DropIndex(name: "IX_StudioUser_TenantId_GoogleSubject", table: "StudioUser");
        migrationBuilder.Sql("""
            UPDATE "StudioUser" AS current
            SET "GoogleSubject" = NULL
            WHERE "GoogleSubject" IS NOT NULL AND EXISTS (
                SELECT 1 FROM "StudioUser" AS earlier
                WHERE earlier."GoogleSubject" = current."GoogleSubject"
                  AND (earlier."TenantId" < current."TenantId" OR
                      (earlier."TenantId" = current."TenantId" AND earlier."Id" < current."Id")))
            """);
        migrationBuilder.CreateIndex(name: "IX_StudioUser_GoogleSubject", table: "StudioUser", column: "GoogleSubject", unique: true);
        migrationBuilder.DropColumn(name: "CancelledAt", table: "StudioInvitation");
        migrationBuilder.DropTable(name: "GoogleIdentities");
    }
}
