using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MtgDecker.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCardPrices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "PriceEur",
                table: "Cards",
                type: "decimal(10,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PriceEurFoil",
                table: "Cards",
                type: "decimal(10,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PriceTix",
                table: "Cards",
                type: "decimal(10,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PriceUsd",
                table: "Cards",
                type: "decimal(10,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PriceUsdFoil",
                table: "Cards",
                type: "decimal(10,2)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PriceEur",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "PriceEurFoil",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "PriceTix",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "PriceUsd",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "PriceUsdFoil",
                table: "Cards");
        }
    }
}
