using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Persistance.Migrations
{
    /// <inheritdoc />
    public partial class AddMovieFriendsSharing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ImportedFromMovieId",
                table: "Movies",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MovieShareSettings",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "text", nullable: false),
                    ShareMovies = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MovieShareSettings", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_MovieShareSettings_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Movies_UserId_ImportedFromMovieId",
                table: "Movies",
                columns: new[] { "UserId", "ImportedFromMovieId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MovieShareSettings");

            migrationBuilder.DropIndex(
                name: "IX_Movies_UserId_ImportedFromMovieId",
                table: "Movies");

            migrationBuilder.DropColumn(
                name: "ImportedFromMovieId",
                table: "Movies");
        }
    }
}
