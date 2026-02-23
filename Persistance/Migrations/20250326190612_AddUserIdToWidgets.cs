using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Persistance.Migrations
{
    /// <inheritdoc />
    public partial class AddUserIdToWidgets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "UserId",
                table: "MainWidgets",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_MainWidgets_UserId",
                table: "MainWidgets",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_MainWidgets_AspNetUsers_UserId",
                table: "MainWidgets",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MainWidgets_AspNetUsers_UserId",
                table: "MainWidgets");

            migrationBuilder.DropIndex(
                name: "IX_MainWidgets_UserId",
                table: "MainWidgets");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "MainWidgets");
        }
    }
}
