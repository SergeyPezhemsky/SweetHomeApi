using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Persistance.Migrations
{
    /// <inheritdoc />
    public partial class AddWardrobe : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WardrobeItems",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Category = table.Column<string>(type: "text", nullable: false),
                    Colors = table.Column<List<string>>(type: "text[]", nullable: false),
                    Seasons = table.Column<List<string>>(type: "text[]", nullable: false),
                    Styles = table.Column<List<string>>(type: "text[]", nullable: false),
                    Warmth = table.Column<int>(type: "integer", nullable: false),
                    Brand = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IsFavorite = table.Column<bool>(type: "boolean", nullable: false),
                    HasImage = table.Column<bool>(type: "boolean", nullable: false),
                    ImageUpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    WearCount = table.Column<int>(type: "integer", nullable: false),
                    LastWornAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WardrobeItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WardrobeItems_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WardrobeOutfits",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    ItemIds = table.Column<List<string>>(type: "text[]", nullable: false),
                    Occasion = table.Column<string>(type: "text", nullable: false),
                    Temperature = table.Column<int>(type: "integer", nullable: true),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    WearCount = table.Column<int>(type: "integer", nullable: false),
                    LastWornAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WardrobeOutfits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WardrobeOutfits_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WardrobeItemImages",
                columns: table => new
                {
                    ItemId = table.Column<string>(type: "text", nullable: false),
                    Data = table.Column<byte[]>(type: "bytea", nullable: false),
                    ContentType = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WardrobeItemImages", x => x.ItemId);
                    table.ForeignKey(
                        name: "FK_WardrobeItemImages_WardrobeItems_ItemId",
                        column: x => x.ItemId,
                        principalTable: "WardrobeItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WardrobeItems_CreatedAt",
                table: "WardrobeItems",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_WardrobeItems_UserId",
                table: "WardrobeItems",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_WardrobeItems_UserId_Category",
                table: "WardrobeItems",
                columns: new[] { "UserId", "Category" });

            migrationBuilder.CreateIndex(
                name: "IX_WardrobeOutfits_CreatedAt",
                table: "WardrobeOutfits",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_WardrobeOutfits_UserId",
                table: "WardrobeOutfits",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WardrobeItemImages");

            migrationBuilder.DropTable(
                name: "WardrobeOutfits");

            migrationBuilder.DropTable(
                name: "WardrobeItems");
        }
    }
}
