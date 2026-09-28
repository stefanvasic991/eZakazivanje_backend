using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eZakazivanje.DataService.Migrations
{
    /// <inheritdoc />
    public partial class AddCurrencyToService : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "Services",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Currency",
                table: "Services");
        }
    }
}
