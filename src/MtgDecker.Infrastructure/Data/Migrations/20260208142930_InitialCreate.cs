using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MtgDecker.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BulkDataImports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ImportedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ScryfallDataType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CardCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BulkDataImports", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Cards",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ScryfallId = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: false),
                    OracleId = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    ManaCost = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Cmc = table.Column<double>(type: "float", nullable: false),
                    TypeLine = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    OracleText = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Colors = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ColorIdentity = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Rarity = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SetCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    SetName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CollectorNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ImageUri = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ImageUriSmall = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ImageUriArtCrop = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Layout = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cards", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CollectionEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CardId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    IsFoil = table.Column<bool>(type: "bit", nullable: false),
                    Condition = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CollectionEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Decks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Format = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Decks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CardFaces",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CardId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    ManaCost = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    TypeLine = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    OracleText = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ImageUri = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Power = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    Toughness = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CardFaces", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CardFaces_Cards_CardId",
                        column: x => x.CardId,
                        principalTable: "Cards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CardLegalities",
                columns: table => new
                {
                    FormatName = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    CardId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CardLegalities", x => new { x.CardId, x.FormatName });
                    table.ForeignKey(
                        name: "FK_CardLegalities_Cards_CardId",
                        column: x => x.CardId,
                        principalTable: "Cards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DeckEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeckId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CardId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    Category = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeckEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeckEntries_Decks_DeckId",
                        column: x => x.DeckId,
                        principalTable: "Decks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CardFaces_CardId",
                table: "CardFaces",
                column: "CardId");

            migrationBuilder.CreateIndex(
                name: "IX_Cards_Name",
                table: "Cards",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Cards_OracleId",
                table: "Cards",
                column: "OracleId");

            migrationBuilder.CreateIndex(
                name: "IX_Cards_ScryfallId",
                table: "Cards",
                column: "ScryfallId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Cards_SetCode",
                table: "Cards",
                column: "SetCode");

            migrationBuilder.CreateIndex(
                name: "IX_CollectionEntries_CardId",
                table: "CollectionEntries",
                column: "CardId");

            migrationBuilder.CreateIndex(
                name: "IX_CollectionEntries_UserId",
                table: "CollectionEntries",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_DeckEntries_CardId",
                table: "DeckEntries",
                column: "CardId");

            migrationBuilder.CreateIndex(
                name: "IX_DeckEntries_DeckId",
                table: "DeckEntries",
                column: "DeckId");

            migrationBuilder.CreateIndex(
                name: "IX_Decks_UserId",
                table: "Decks",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BulkDataImports");

            migrationBuilder.DropTable(
                name: "CardFaces");

            migrationBuilder.DropTable(
                name: "CardLegalities");

            migrationBuilder.DropTable(
                name: "CollectionEntries");

            migrationBuilder.DropTable(
                name: "DeckEntries");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "Cards");

            migrationBuilder.DropTable(
                name: "Decks");
        }
    }
}
