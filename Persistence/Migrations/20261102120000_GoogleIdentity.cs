using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudioCore.Persistence.Migrations;

[DbContext(typeof(StudioDbContext))]
[Migration("20261102120000_GoogleIdentity")]
public sealed class GoogleIdentity : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "GoogleSubject",
            table: "StudioUser",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_StudioUser_GoogleSubject",
            table: "StudioUser",
            column: "GoogleSubject",
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "IX_StudioUser_GoogleSubject", table: "StudioUser");
        migrationBuilder.DropColumn(name: "GoogleSubject", table: "StudioUser");
    }
}
