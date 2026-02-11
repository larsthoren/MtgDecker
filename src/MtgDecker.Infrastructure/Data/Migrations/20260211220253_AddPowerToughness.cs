using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MtgDecker.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPowerToughness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Power",
                table: "Cards",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Toughness",
                table: "Cards",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Power",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "Toughness",
                table: "Cards");
        }
    }
}
