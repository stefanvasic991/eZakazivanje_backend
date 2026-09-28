using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eZakazivanje.DataService.Migrations
{
    /// <inheritdoc />
    public partial class AddDayOfWeekToBusinessSchedule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DayOfWeek",
                table: "BusinessSchedules",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DayOfWeek",
                table: "BusinessSchedules");
        }
    }
}
