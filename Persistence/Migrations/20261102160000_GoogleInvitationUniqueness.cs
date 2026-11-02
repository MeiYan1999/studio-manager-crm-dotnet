using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudioCore.Persistence.Migrations;

[DbContext(typeof(StudioDbContext))]
[Migration("20261102160000_GoogleInvitationUniqueness")]
public sealed class GoogleInvitationUniqueness : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "PendingKey",
            table: "StudioInvitation",
            nullable: true);

        migrationBuilder.Sql("""
            UPDATE "StudioInvitation"
            SET "PendingKey" = lower("Email")
            WHERE "AcceptedAt" IS NULL AND "CancelledAt" IS NULL
              AND "Id" = (
                  SELECT MIN(other."Id")
                  FROM "StudioInvitation" AS other
                  WHERE other."TenantId" = "StudioInvitation"."TenantId"
                    AND lower(other."Email") = lower("StudioInvitation"."Email")
                    AND other."AcceptedAt" IS NULL
                    AND other."CancelledAt" IS NULL)
            """);

        migrationBuilder.CreateIndex(
            name: "IX_StudioInvitation_TenantId_PendingKey",
            table: "StudioInvitation",
            columns: new[] { "TenantId", "PendingKey" },
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_StudioInvitation_TenantId_PendingKey",
            table: "StudioInvitation");
        migrationBuilder.DropColumn(name: "PendingKey", table: "StudioInvitation");
    }
}
