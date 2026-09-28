using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eZakazivanje.DataService.Migrations
{
    /// <inheritdoc />
    public partial class AddNoteAndServiceNameToFreeTimeSlot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Note",
                table: "FreeTimeSlots",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ServiceName",
                table: "FreeTimeSlots",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Note",
                table: "FreeTimeSlots");

            migrationBuilder.DropColumn(
                name: "ServiceName",
                table: "FreeTimeSlots");
        }
    }
}
