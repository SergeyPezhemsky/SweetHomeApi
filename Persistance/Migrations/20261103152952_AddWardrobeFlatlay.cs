using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Persistance.Migrations
{
    /// <inheritdoc />
    public partial class AddWardrobeFlatlay : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ImageStatus",
                table: "WardrobeItems",
                type: "text",
                nullable: false,
                defaultValue: "NONE");

            migrationBuilder.AddColumn<string>(
                name: "ImageVariant",
                table: "WardrobeItems",
                type: "text",
                nullable: false,
                defaultValue: "ORIGINAL");

            migrationBuilder.AddColumn<string>(
                name: "ProcessedContentType",
                table: "WardrobeItemImages",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "ProcessedData",
                table: "WardrobeItemImages",
                type: "bytea",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProcessingStatus",
                table: "WardrobeItemImages",
                type: "text",
                nullable: false,
                defaultValue: "NONE");

            migrationBuilder.AddColumn<int>(
                name: "Revision",
                table: "WardrobeItemImages",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_WardrobeItemImages_ProcessingStatus",
                table: "WardrobeItemImages",
                column: "ProcessingStatus");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WardrobeItemImages_ProcessingStatus",
                table: "WardrobeItemImages");

            migrationBuilder.DropColumn(
                name: "ImageStatus",
                table: "WardrobeItems");

            migrationBuilder.DropColumn(
                name: "ImageVariant",
                table: "WardrobeItems");

            migrationBuilder.DropColumn(
                name: "ProcessedContentType",
                table: "WardrobeItemImages");

            migrationBuilder.DropColumn(
                name: "ProcessedData",
                table: "WardrobeItemImages");

            migrationBuilder.DropColumn(
                name: "ProcessingStatus",
                table: "WardrobeItemImages");

            migrationBuilder.DropColumn(
                name: "Revision",
                table: "WardrobeItemImages");
        }
    }
}
