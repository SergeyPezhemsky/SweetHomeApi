using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Persistance.Migrations
{
    /// <inheritdoc />
    public partial class AddFinance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Ревизии чеков: результат распознавания сохраняется только для той версии чека, которую отправляли.
            migrationBuilder.Sql("""CREATE SEQUENCE IF NOT EXISTS "FinanceReceiptRevisionSeq" AS integer CYCLE;""");

            migrationBuilder.CreateTable(
                name: "FinanceCategoryRules",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "text", nullable: false),
                    Key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Category = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinanceCategoryRules", x => new { x.UserId, x.Key });
                    table.ForeignKey(
                        name: "FK_FinanceCategoryRules_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FinanceReceipts",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Source = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    FailureReason = table.Column<string>(type: "text", nullable: true),
                    PurchasedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    Store = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Total = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Category = table.Column<string>(type: "text", nullable: false),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    HasImage = table.Column<bool>(type: "boolean", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    RecognitionJson = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinanceReceipts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FinanceReceipts_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FinanceReceiptImages",
                columns: table => new
                {
                    ReceiptId = table.Column<string>(type: "text", nullable: false),
                    Data = table.Column<byte[]>(type: "bytea", nullable: false),
                    ContentType = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinanceReceiptImages", x => x.ReceiptId);
                    table.ForeignKey(
                        name: "FK_FinanceReceiptImages_FinanceReceipts_ReceiptId",
                        column: x => x.ReceiptId,
                        principalTable: "FinanceReceipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FinanceReceiptItems",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    ReceiptId = table.Column<string>(type: "text", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: true),
                    Price = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    Category = table.Column<string>(type: "text", nullable: false),
                    CategorySource = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinanceReceiptItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FinanceReceiptItems_FinanceReceipts_ReceiptId",
                        column: x => x.ReceiptId,
                        principalTable: "FinanceReceipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceReceiptItems_ReceiptId",
                table: "FinanceReceiptItems",
                column: "ReceiptId");

            migrationBuilder.CreateIndex(
                name: "IX_FinanceReceipts_Status",
                table: "FinanceReceipts",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_FinanceReceipts_UserId_PurchasedOn",
                table: "FinanceReceipts",
                columns: new[] { "UserId", "PurchasedOn" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""DROP SEQUENCE IF EXISTS "FinanceReceiptRevisionSeq";""");

            migrationBuilder.DropTable(
                name: "FinanceCategoryRules");

            migrationBuilder.DropTable(
                name: "FinanceReceiptImages");

            migrationBuilder.DropTable(
                name: "FinanceReceiptItems");

            migrationBuilder.DropTable(
                name: "FinanceReceipts");
        }
    }
}
