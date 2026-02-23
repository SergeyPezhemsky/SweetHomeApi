using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Persistance.Migrations;

[DbContext(typeof(SweetHomeDbContext))]
[Migration("20260223150000_AddHealthEntries")]
public class AddHealthEntries : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "HealthEntries",
            columns: table => new
            {
                Id = table.Column<string>(type: "text", nullable: false),
                Date = table.Column<DateOnly>(type: "date", nullable: false),
                UserId = table.Column<string>(type: "text", nullable: false),
                Weight = table.Column<string>(type: "text", nullable: true),
                BloodPressure = table.Column<string>(type: "text", nullable: true),
                BloodSugar = table.Column<string>(type: "text", nullable: true),
                Water = table.Column<string>(type: "text", nullable: true),
                Temperature = table.Column<string>(type: "text", nullable: true),
                Monthlies = table.Column<bool>(type: "boolean", nullable: false),
                DictionaryStateJson = table.Column<string>(type: "text", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_HealthEntries", x => x.Id);
                table.ForeignKey(
                    name: "FK_HealthEntries_AspNetUsers_UserId",
                    column: x => x.UserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_HealthEntries_UserId_Date",
            table: "HealthEntries",
            columns: new[] { "UserId", "Date" },
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "HealthEntries");
    }
}
