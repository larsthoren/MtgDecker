using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MtgDecker.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class PostHardeningSync : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Remove duplicate collection entries before creating unique index
            migrationBuilder.Sql(@"
                WITH cte AS (
                    SELECT Id, ROW_NUMBER() OVER (
                        PARTITION BY UserId, CardId, IsFoil, Condition
                        ORDER BY Id
                    ) AS rn
                    FROM CollectionEntries
                )
                DELETE FROM cte WHERE rn > 1;
            ");

            migrationBuilder.CreateIndex(
                name: "IX_CollectionEntries_UserId_CardId_IsFoil_Condition",
                table: "CollectionEntries",
                columns: new[] { "UserId", "CardId", "IsFoil", "Condition" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Cards_Cmc",
                table: "Cards",
                column: "Cmc");

            migrationBuilder.CreateIndex(
                name: "IX_Cards_Rarity",
                table: "Cards",
                column: "Rarity");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CollectionEntries_UserId_CardId_IsFoil_Condition",
                table: "CollectionEntries");

            migrationBuilder.DropIndex(
                name: "IX_Cards_Cmc",
                table: "Cards");

            migrationBuilder.DropIndex(
                name: "IX_Cards_Rarity",
                table: "Cards");
        }
    }
}
