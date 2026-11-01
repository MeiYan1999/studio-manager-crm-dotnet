using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudioCore.Persistence.Migrations;

[DbContext(typeof(StudioDbContext))]
[Migration("20261101160000_AccountLifecycle")]
public sealed class AccountLifecycle : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "IsEmailVerified",
            table: "StudioUser",
            nullable: false,
            defaultValue: true);

        migrationBuilder.CreateTable(
            name: "AccountToken",
            columns: table => new
            {
                TenantId = table.Column<string>(nullable: false, defaultValue: "system-master"),
                Id = table.Column<string>(nullable: false),
                UserId = table.Column<string>(nullable: false),
                Purpose = table.Column<string>(nullable: false),
                TokenHash = table.Column<string>(nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(nullable: false),
                ExpiresAt = table.Column<DateTimeOffset>(nullable: false),
                UsedAt = table.Column<DateTimeOffset>(nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AccountToken", x => new { x.TenantId, x.Id });
                table.ForeignKey(
                    name: "FK_AccountToken_Studios_TenantId",
                    column: x => x.TenantId,
                    principalTable: "Studios",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_AccountToken_TenantId_UserId_Purpose",
            table: "AccountToken",
            columns: new[] { "TenantId", "UserId", "Purpose" });

        migrationBuilder.CreateIndex(
            name: "IX_AccountToken_TokenHash",
            table: "AccountToken",
            column: "TokenHash",
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "AccountToken");
        migrationBuilder.DropColumn(name: "IsEmailVerified", table: "StudioUser");
    }
}
