using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Persistance.Migrations
{
    /// <inheritdoc />
    public partial class AddAliasToMainWidgets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Alias",
                table: "MainWidgets",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql("UPDATE \"MainWidgets\" SET \"Alias\" = 'home' WHERE \"Name\" = 'Дом' AND \"Alias\" = '';");
            migrationBuilder.Sql("UPDATE \"MainWidgets\" SET \"Alias\" = 'movies' WHERE \"Name\" = 'Кино' AND \"Alias\" = '';");
            migrationBuilder.Sql("UPDATE \"MainWidgets\" SET \"Alias\" = 'books' WHERE \"Name\" = 'Книги' AND \"Alias\" = '';");
            migrationBuilder.Sql("UPDATE \"MainWidgets\" SET \"Alias\" = 'trips' WHERE \"Name\" = 'Путешествия' AND \"Alias\" = '';");
            migrationBuilder.Sql("UPDATE \"MainWidgets\" SET \"Alias\" = 'coins' WHERE \"Name\" = 'Монеты' AND \"Alias\" = '';");
            migrationBuilder.Sql("UPDATE \"MainWidgets\" SET \"Alias\" = 'health' WHERE \"Name\" = 'Здоровье' AND \"Alias\" = '';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Alias",
                table: "MainWidgets");
        }
    }
}
